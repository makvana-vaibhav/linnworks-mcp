#!/usr/bin/env python3
"""Exercise every registered tool against the schema-driven stub."""
import json, urllib.request, mcpcall

LW = {"X-Linnworks-User-Id": "audit", "X-Linnworks-Application-Id": "app",
      "X-Linnworks-Application-Secret": "sec", "X-Linnworks-Token": "tok"}
UUID = "b9a98b1b-9ffd-5c83-81f6-086d924ab8bc"

# tool -> (args, fields that must be populated for the mapping to be proven correct)
CASES = [
    ("get_locations",            {"pageSize": 5},                       ["items.0.stockLocationId","items.0.locationName","totalCount"]),
    ("get_location_by_id",       {"stockLocationId": UUID},             ["stockLocationId","locationName"]),
    ("get_inventory_items",      {"pageSize": 5},                       ["items.0.stockItemId","items.0.sku","items.0.title","items.0.retailPrice","items.0.minimumLevel"]),
    ("get_inventory_items",      {"searchKeyword":"widget","pageSize":5},["items.0.sku"]),
    ("get_inventory_item_by_id", {"stockItemId": UUID},                 ["stockItemId","sku","title","retailPrice","taxRate"]),
    ("get_low_stock_items",      {"pageSize": 5},                       ["items.0.sku","items.0.title","items.0.minimumLevel","pagingNote"]),
    ("get_low_stock_items",      {"locationId": UUID,"pageSize":5},     ["items.0.sku"]),
    ("get_stock_levels",         {"stockItemIds": UUID},                ["0.stockItemId","0.locationName","0.quantity"]),
    ("get_stock_by_location",    {"locationId": UUID,"stockItemIds":UUID},[]),
    ("get_open_orders",          {"pageSize": 5},                       ["items.0.orderId","items.0.numOrderId","items.0.status","items.0.totalCharge","totalCount"]),
    ("get_open_orders",          {"locationId": UUID,"pageSize":5},     ["items.0.orderId"]),
    ("get_order_by_id",          {"orderIds": UUID},                    ["0.orderId","0.numOrderId","0.status","0.items.0.sku"]),
    ("get_unfulfilled_orders",   {"pageSize": 5},                       ["items.0.orderId","totalCount"]),
    ("update_stock_levels",      {"sku":"SKU-1","locationId":UUID,"quantity":7}, None),  # gated: expect refusal
]

def dig(obj, path):
    for part in path.split("."):
        if obj is None: return None
        obj = obj[int(part)] if part.isdigit() else obj.get(part)
    return obj

print("=== DYNAMIC AUDIT: every registered tool vs schema-accurate responses ===\n")
fails = 0
for tool, args, required in CASES:
    r = mcpcall.tool(tool, args, lw_headers=LW)
    res = r.get("result", {})
    text = (res.get("content") or [{}])[0].get("text", "")
    label = f"{tool}({','.join(args)})"

    if required is None:                       # destructive tool must be refused
        ok = res.get("isError") and "disabled on this server" in text
        print(f"  [{'PASS' if ok else 'FAIL'}] {label:52} gated as destructive")
        fails += not ok
        continue

    if res.get("isError"):
        print(f"  [FAIL] {label:52} {text[:110]}")
        fails += 1
        continue
    try:
        d = json.loads(text)
    except Exception as e:
        print(f"  [FAIL] {label:52} unparseable JSON: {e}")
        fails += 1
        continue

    missing = [f for f in required if dig(d, f) in (None, "", 0)]
    if missing:
        print(f"  [FAIL] {label:52} unmapped/default fields: {missing}")
        fails += 1
    else:
        print(f"  [PASS] {label:52} {len(text)}B")

print(f"\n{len(CASES)-fails}/{len(CASES)} passed")

print("\n=== requests actually sent to Linnworks ===")
for c in json.load(urllib.request.urlopen("http://127.0.0.1:5199/__captured")):
    detail = json.dumps(c.get("body")) if c["verb"] == "POST" else "?" + c.get("query","")
    print(f"  {c['verb']:4} {c['path']:38} {detail[:100]}")
