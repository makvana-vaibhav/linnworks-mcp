#!/usr/bin/env python3
"""Audit the cross-cutting behaviour: validation, auth, caching, isolation, resilience."""
import json, urllib.request, uuid, mcpcall

RUN = uuid.uuid4().hex[:8]  # unique tenants: the session cache now genuinely persists

STUB = "http://127.0.0.1:5199"
UUID = "b9a98b1b-9ffd-5c83-81f6-086d924ab8bc"

def stub(p, body=None):
    if body is None:
        return json.load(urllib.request.urlopen(f"{STUB}{p}"))
    r = urllib.request.Request(f"{STUB}{p}", data=json.dumps(body).encode(),
                               headers={"Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(r))

def creds(user, token="tok"):
    return {"X-Linnworks-User-Id": user, "X-Linnworks-Application-Id": "app",
            "X-Linnworks-Application-Secret": "sec", "X-Linnworks-Token": token}

results = []
def check(name, ok, detail=""):
    results.append(ok)
    print(f"  [{'PASS' if ok else 'FAIL'}] {name}" + (f"  — {detail}" if detail and not ok else ""))

def text_of(r):
    res = r.get("result", {})
    return res.get("isError"), (res.get("content") or [{}])[0].get("text", "")

print("=== VALIDATION (must reject before any upstream call) ===")
stub("/__reset")
for args, expect in [
    ({"pageSize": 500},        "must not exceed 200"),
    ({"pageSize": 0},          "must be 1 or greater"),
    ({"pageNumber": 0},        "must be 1 or greater"),
]:
    err, t = text_of(mcpcall.tool("get_inventory_items", args, lw_headers=creds(f"u1-{RUN}")))
    check(f"get_inventory_items{tuple(args.items())[0]}", bool(err) and expect in t, t[:90])
err, t = text_of(mcpcall.tool("get_inventory_item_by_id", {"stockItemId": "nope"}, lw_headers=creds(f"u1-{RUN}")))
check("bad UUID rejected", bool(err) and "valid UUID" in t, t[:90])
check("no upstream call made during validation failures",
      len(stub("/__captured")) == 0, f"{len(stub('/__captured'))} calls leaked")

print("\n=== CLIENT AUTH (discovery open, execution gated) ===")
# Discovery must answer connector probes that have no key yet.
r = mcpcall.call("tools/list", api_key=None)
check("anonymous discovery allowed (connector probe)", "result" in r, str(r)[:90])
r = mcpcall.call("tools/list")
check("discovery with valid key allowed", "result" in r, str(r)[:90])
# Execution is the part that must stay locked.
ex = lambda k: mcpcall.tool("get_locations", {}, api_key=k, lw_headers=creds(f"authprobe-{RUN}"))
check("execution without a key refused", ex(None).get("httpStatus") == 401, str(ex(None))[:90])
check("execution with a wrong key refused", ex("wrong-key").get("httpStatus") == 401, str(ex("wrong-key"))[:90])
_ok = ex("dev-local-key")
check("execution with a valid key allowed",
      "result" in _ok and not _ok["result"].get("isError"), str(_ok)[:110])

print("\n=== LINNWORKS CREDENTIALS ===")
err, t = text_of(mcpcall.tool("get_locations", {}))
check("missing credential headers give a named error",
      bool(err) and "credentials were not supplied" in t, t[:120])

print("\n=== SESSION CACHING & TENANT ISOLATION ===")
stub("/__reset")
for _ in range(3):
    mcpcall.tool("get_locations", {}, lw_headers=creds(f"alice-{RUN}", "alice-token"))
n_auth = len(stub("/__auth"))
check("3 calls for one tenant authorize only once", n_auth == 1, f"authorized {n_auth}x")

mcpcall.tool("get_locations", {}, lw_headers=creds(f"bob-{RUN}", "bob-token"))
auths = stub("/__auth")
check("a second tenant triggers its own authorize", len(auths) == 2, f"{len(auths)} auths")
check("each tenant authorizes with its own token",
      {a["Token"] for a in auths} == {"alice-token", "bob-token"},
      str([a["Token"] for a in auths]))

sent = [c for c in stub("/__captured") if c.get("auth")]
alice_tok, bob_tok = "sess-alice-token", "sess-bob-token"
check("tenants' session tokens never cross",
      {c["auth"] for c in sent} <= {alice_tok, bob_tok} and len({c["auth"] for c in sent}) == 2,
      str({c["auth"] for c in sent}))

stub("/__reset")
mcpcall.tool("get_locations", {}, lw_headers=creds(f"alice-{RUN}", "DIFFERENT-token"))
check("same user id with changed credentials re-authorizes",
      len(stub("/__auth")) == 1 and stub("/__auth")[0]["Token"] == "DIFFERENT-token")

print("\n=== RESILIENCE ===")
stub("/__reset")
stub("/__fault", {"429": 2})
err, t = text_of(mcpcall.tool("get_open_orders", {"pageSize": 2}, lw_headers=creds(f"carol-{RUN}")))
calls = [c for c in stub("/__captured") if "GetOpenOrders" in c["path"]]
check("429 with Retry-After is retried and then succeeds",
      not err and len(calls) == 3, f"isError={err} attempts={len(calls)}")

stub("/__reset")
stub("/__fault", {"401": 1})
err, t = text_of(mcpcall.tool("get_open_orders", {"pageSize": 2}, lw_headers=creds(f"dave-{RUN}")))
check("401 invalidates the session and re-authorizes once",
      not err and len(stub("/__auth")) == 2, f"isError={err} auths={len(stub('/__auth'))}")

stub("/__reset")
stub("/__fault", {"429": 9})
err, t = text_of(mcpcall.tool("get_open_orders", {"pageSize": 2}, lw_headers=creds(f"erin-{RUN}")))
check("retries are capped and surface a clean rate-limit message",
      bool(err) and "rate-limiting" in t, t[:110])

print(f"\n{sum(results)}/{len(results)} passed")
