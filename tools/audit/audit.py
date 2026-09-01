import json, re

SPECS     = json.load(open("endpoints.json"))
CODE      = json.load(open("code_contracts.json"))
CLASSES   = CODE["classes"]
CALLS     = CODE["calls"]

CS2JSON = {
    "int":"integer","int?":"integer","long":"integer","long?":"integer",
    "string":"string","string?":"string",
    "bool":"boolean","bool?":"boolean",
    "decimal":"number","decimal?":"number","double":"number","double?":"number",
    "DateTimeOffset?":"string","DateTimeOffset":"string","DateTime?":"string",
}
def cs_json_type(t):
    if t in CS2JSON: return CS2JSON[t]
    if t.startswith(("List<","IReadOnlyList<")) or t.endswith("[]"): return "array"
    return "object"

def load(slug):
    return json.load(open(f"spec/{slug}.json"))

def deref(spec, node):
    seen = 0
    while isinstance(node, dict) and "$ref" in node and seen < 10:
        node = spec["components"]["schemas"][node["$ref"].split("/")[-1]]; seen += 1
    return node or {}

def op(spec, path):
    d = spec["paths"][path]
    return next((d[v], v.upper()) for v in ("get","post","put","delete") if v in d)

def req_schema(spec, o):
    rb = o.get("requestBody")
    if not rb: return None
    if "$ref" in rb:
        rb = spec["components"]["requestBodies"][rb["$ref"].split("/")[-1]]
    return deref(spec, rb["content"]["application/json"]["schema"])

def resp_schema(spec, o):
    s = o["responses"]["200"]["content"]["application/json"]["schema"]
    s = deref(spec, s)
    if s.get("type") == "array":
        return deref(spec, s.get("items", {})), True
    return s, False

issues = []
def report(ep, level, msg):
    issues.append((ep, level, msg))

def check_props(ep, kind, code_props, spec_props, spec_required=()):
    for jsonname, ctype in code_props.items():
        if jsonname not in spec_props:
            # case-insensitive near-miss is a strong signal of a real bug
            near = [k for k in spec_props if k.lower() == jsonname.lower()]
            if near:
                report(ep,"ERROR",f"{kind} field '{jsonname}' has wrong casing; spec says '{near[0]}'")
            else:
                report(ep,"WARN", f"{kind} field '{jsonname}' not in spec (ignored by server)")
            continue
        want = spec_props[jsonname].get("type")
        got  = cs_json_type(ctype)
        if want and got != want and not (want=="string" and got=="string"):
            if {want,got} == {"number","integer"}:
                continue   # numeric widening is safe
            report(ep,"ERROR",f"{kind} field '{jsonname}': code={ctype}({got}) but spec={want}")
    for r in spec_required or ():
        if r not in code_props:
            report(ep,"ERROR",f"request is missing REQUIRED field '{r}'")

for path, slug in SPECS.items():
    spec = load(slug)
    o, spec_verb = op(spec, path)
    calls = CALLS.get(path, [])
    if not calls:
        report(path,"ERROR","endpoint constant present but no call site found"); continue
    c = calls[0]
    if c["verb"] != spec_verb:
        report(path,"ERROR",f"code uses {c['verb']} but spec declares {spec_verb}")

    # ---- request
    rs = req_schema(spec, o)
    if c["request"] and rs:
        cp = CLASSES.get(c["request"], {})
        check_props(path,"request", cp, rs.get("properties",{}), rs.get("required",[]))
    elif c["request"] and not rs:
        report(path,"ERROR",f"code POSTs {c['request']} but spec declares no request body")

    # ---- response
    rsp, is_array = resp_schema(spec, o)
    cls = re.sub(r"^(List|IReadOnlyList)<(.+)>$", r"\2", c["response"])
    envelope = re.match(r"^(\w+)<(\w+)>$", cls)
    if envelope:                        # e.g. PostFilterPagedResponse<OrderDetailsResponse>
        check_props(path,"response(envelope)", CLASSES.get(envelope.group(1),{}), rsp.get("properties",{}))
        inner = deref(spec, rsp.get("properties",{}).get("Data",{}).get("items",{}))
        check_props(path,"response(item)", CLASSES.get(envelope.group(2),{}), inner.get("properties",{}))
    else:
        code_is_array = c["response"].startswith(("List<","IReadOnlyList<"))
        if is_array != code_is_array:
            report(path,"ERROR",
                   f"response shape mismatch: spec returns {'array' if is_array else 'object'}, "
                   f"code expects {'array' if code_is_array else 'object'}")
        check_props(path,"response", CLASSES.get(cls,{}), rsp.get("properties",{}))

errs  = [i for i in issues if i[1]=="ERROR"]
warns = [i for i in issues if i[1]=="WARN"]
print(f"=== STATIC AUDIT: {len(SPECS)} endpoints — {len(errs)} errors, {len(warns)} warnings ===\n")
for ep in SPECS:
    mine = [i for i in issues if i[0]==ep]
    status = "FAIL" if any(i[1]=="ERROR" for i in mine) else ("warn" if mine else "PASS")
    print(f"[{status}] {ep}")
    for _,lvl,msg in mine:
        print(f"        {lvl}: {msg}")
