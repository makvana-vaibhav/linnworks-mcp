#!/usr/bin/env python3
"""Drive the server over stdio, the way a desktop MCP client launching it as a subprocess would."""
import json, subprocess, sys, os

PROJ = "/Users/cypherops/Rishvi/Projects/rishvi-agent (1)/LinnworksMcp/src/LinnworksMcp/LinnworksMcp.csproj"

env = dict(os.environ,
           Linnworks__AuthUrl="http://127.0.0.1:5199/api/Auth/AuthorizeByApplication",
           Linnworks__Stdio__UserId="stdio-user",
           Linnworks__Stdio__ApplicationId="app",
           Linnworks__Stdio__ApplicationSecret="sec",
           Linnworks__Stdio__Token="stdio-token")

proc = subprocess.Popen(["dotnet", "run", "--project", PROJ, "--", "--stdio"],
                        stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                        stderr=subprocess.DEVNULL, text=True, bufsize=1, env=env)

def send(m): proc.stdin.write(json.dumps(m) + "\n"); proc.stdin.flush()
def read():
    while True:
        line = proc.stdout.readline()
        if not line: return None
        if line.strip().startswith("{"): return json.loads(line)

send({"jsonrpc":"2.0","id":1,"method":"initialize","params":{
    "protocolVersion":"2025-06-18","capabilities":{},
    "clientInfo":{"name":"stdio-audit","version":"1.0"}}})
init = read()
if not init or "result" not in init:
    print("  [FAIL] initialize:", init); proc.kill(); sys.exit(1)
r = init["result"]
print(f"  [PASS] initialize -> {r['serverInfo']['name']} v{r['serverInfo'].get('version','?')} "
      f"(protocol {r['protocolVersion']})")
print(f"  [{'PASS' if r.get('instructions') else 'FAIL'}] server instructions present")

send({"jsonrpc":"2.0","method":"notifications/initialized"})
send({"jsonrpc":"2.0","id":2,"method":"tools/list"})
tools = read()["result"]["tools"]
print(f"  [{'PASS' if len(tools)==11 else 'FAIL'}] tools/list -> {len(tools)} tools")

send({"jsonrpc":"2.0","id":3,"method":"tools/call",
      "params":{"name":"get_locations","arguments":{"pageSize":3}}})
res = read()["result"]
ok = not res.get("isError")
body = (res.get("content") or [{}])[0].get("text","")
print(f"  [{'PASS' if ok else 'FAIL'}] tools/call get_locations (config credentials) {body[:90]}")

send({"jsonrpc":"2.0","id":4,"method":"tools/call",
      "params":{"name":"get_inventory_items","arguments":{"pageSize":999}}})
res = read()["result"]
t = (res.get("content") or [{}])[0].get("text","")
print(f"  [{'PASS' if res.get('isError') and 'must not exceed 200' in t else 'FAIL'}] "
      f"validation still enforced over stdio")

proc.stdin.close()
try:
    proc.wait(timeout=20)
    print(f"  [{'PASS' if proc.returncode==0 else 'FAIL'}] clean shutdown (exit {proc.returncode})")
except subprocess.TimeoutExpired:
    proc.kill(); print("  [FAIL] shutdown timed out")
