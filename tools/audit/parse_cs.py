"""Extract JSON contracts from the C# models/services so they can be diffed against the specs."""
import re, glob, json, os

SRC = "/Users/cypherops/Rishvi/Projects/rishvi-agent (1)/LinnworksMcp/src/LinnworksMcp"

def csharp_classes():
    """class name -> {json property name: c# type}"""
    out = {}
    for f in glob.glob(f"{SRC}/Models/*.cs") + glob.glob(f"{SRC}/Infrastructure/Linnworks/*.cs"):
        src = open(f, encoding="utf-8").read()
        # split on class/record declarations
        for m in re.finditer(r"(?:internal|public)\s+sealed\s+class\s+(\w+)(?:<\w+>)?[^{]*\{", src):
            name = m.group(1)
            # take the body up to the matching close brace (depth-tracked)
            i, depth = m.end(), 1
            while i < len(src) and depth:
                depth += (src[i] == "{") - (src[i] == "}")
                i += 1
            body = src[m.end():i]
            props = {}
            for pm in re.finditer(
                r'(?:\[JsonPropertyName\("([^"]+)"\)\]\s*)?public\s+(?:required\s+)?'
                r'([\w<>\?\[\]\.]+)\s+(\w+)\s*\{', body):
                jsonname, ctype, propname = pm.group(1), pm.group(2), pm.group(3)
                props[jsonname or propname] = ctype
            if props:
                out[name] = props
    return out

def endpoint_calls():
    """endpoint path -> {verb, request class, response class}"""
    calls = {}
    consts = {}
    for f in glob.glob(f"{SRC}/Application/*/*.cs"):
        src = open(f, encoding="utf-8").read()
        for cm in re.finditer(r'const string (\w+)\s*=\s*"([^"]+)"', src):
            consts[cm.group(1)] = cm.group(2)
        for m in re.finditer(
            r'\.(PostAsync|GetAsync)<([^>]*(?:<[^>]*>)?[^>]*)>\(\s*\n?\s*(\w+)', src):
            verb, generics, constname = m.group(1), m.group(2), m.group(3)
            path = consts.get(constname)
            if not path:
                continue
            parts = [p.strip() for p in re.split(r",(?![^<]*>)", generics)]
            if verb == "PostAsync":
                req, resp = parts[0], parts[1] if len(parts) > 1 else "?"
            else:
                req, resp = None, parts[0]
            calls.setdefault(path, []).append(
                {"verb": "POST" if verb == "PostAsync" else "GET",
                 "request": req, "response": resp, "file": os.path.basename(f)})
    return calls

if __name__ == "__main__":
    json.dump({"classes": csharp_classes(), "calls": endpoint_calls()},
              open("code_contracts.json", "w"), indent=1)
    d = json.load(open("code_contracts.json"))
    print(f"parsed {len(d['classes'])} model classes, {len(d['calls'])} endpoints\n")
    for path, cs in d["calls"].items():
        for c in cs:
            print(f"  {c['verb']:4} {path:38} req={c['request']}  resp={c['response']}")
