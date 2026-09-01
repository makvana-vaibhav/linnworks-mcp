"""Minimal Streamable HTTP MCP client for smoke-testing the server."""
import json, urllib.request, urllib.error

BASE = "http://127.0.0.1:5177/mcp"
PROTO = "2026-07-28"

def call(method, params=None, api_key="dev-local-key", lw_headers=None):
    body = {"jsonrpc":"2.0","id":1,"method":method,
            "params":{"_meta":{"io.modelcontextprotocol/protocolVersion":PROTO,
                               "io.modelcontextprotocol/clientCapabilities":{}},
                      **(params or {})}}
    headers = {"Content-Type":"application/json",
               "Accept":"application/json, text/event-stream",
               "MCP-Protocol-Version":PROTO, "Mcp-Method":method}
    if params and "name" in params:
        headers["Mcp-Name"] = params["name"]
    if api_key:
        headers["Authorization"] = f"Bearer {api_key}"
    headers.update(lw_headers or {})
    req = urllib.request.Request(BASE, data=json.dumps(body).encode(),
                                 headers=headers, method="POST")
    try:
        raw = urllib.request.urlopen(req).read().decode()
    except urllib.error.HTTPError as e:
        return {"httpStatus": e.code, "detail": e.read().decode()[:300]}
    for line in raw.splitlines():
        if line.startswith("data: "):
            return json.loads(line[6:])
    return json.loads(raw) if raw.strip() else {"empty": True}

def tool(name, args, **kw):
    return call("tools/call", {"name": name, "arguments": args}, **kw)
