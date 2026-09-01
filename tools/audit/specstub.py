#!/usr/bin/env python3
"""Linnworks stub whose responses are generated from the real OpenAPI schemas.

Because every field gets the type the spec declares, any type mismatch in the C# models
surfaces as a deserialization failure rather than silently defaulting to 0/null.
"""
import json, threading, uuid, re
from http.server import BaseHTTPRequestHandler, HTTPServer

PORT = 5199
ENDPOINTS = json.load(open("endpoints.json"))
SPECS = {p: json.load(open(f"spec/{s}.json")) for p, s in ENDPOINTS.items()}
captured = []
auth_calls = []          # every AuthorizeByApplication, with the credentials presented
faults = {"429": 0, "401": 0}   # armed via /__fault

def deref(spec, node, depth=0):
    seen = 0
    while isinstance(node, dict) and "$ref" in node and seen < 10:
        node = spec["components"]["schemas"].get(node["$ref"].split("/")[-1], {}); seen += 1
    return node or {}

def example(spec, schema, depth=0, name=""):
    s = deref(spec, schema)
    t = s.get("type")
    if depth > 4:
        return None
    if t == "object" or "properties" in s:
        return {k: example(spec, v, depth + 1, k) for k, v in (s.get("properties") or {}).items()}
    if t == "array":
        return [example(spec, s.get("items", {}), depth + 1, name)] if depth < 3 else []
    if t == "integer":
        return 1
    if t == "number":
        return 19.99
    if t == "boolean":
        return False
    if t == "string":
        if s.get("format") == "uuid":
            return str(uuid.uuid5(uuid.NAMESPACE_DNS, name or "x"))
        if s.get("format") == "date-time":
            return "2026-09-01T10:00:00Z"
        return f"{name or 'value'}-sample"
    return None

def response_for(path):
    spec = SPECS[path]
    op = spec["paths"][path]
    op = op.get("post") or op.get("get")
    schema = op["responses"]["200"]["content"]["application/json"]["schema"]
    s = deref(spec, schema)
    if s.get("type") == "array":
        return [example(spec, s.get("items", {}))]
    body = example(spec, s)
    # Paged envelopes need a plausible total so paging assertions mean something.
    if isinstance(body, dict) and "TotalEntries" in body:
        body["TotalEntries"] = 137
        body["TotalPages"] = 137
    return body

class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass

    def _json(self, obj, code=200):
        b = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(b)))
        self.end_headers()
        self.wfile.write(b)

    def _body(self):
        if (self.headers.get("Transfer-Encoding") or "").lower() == "chunked":
            buf = b""
            while True:
                n = int(self.rfile.readline().split(b";")[0] or b"0", 16)
                if not n:
                    self.rfile.readline(); break
                buf += self.rfile.read(n); self.rfile.readline()
            return buf
        return self.rfile.read(int(self.headers.get("Content-Length") or 0))

    def do_POST(self):
        path = self.path.split("?")[0]
        body = json.loads(self._body() or b"{}")
        if path == "/api/Auth/AuthorizeByApplication":
            auth_calls.append(body)
            # Each tenant gets a distinct token so cross-tenant leakage is detectable.
            # Deterministic across processes so tests can assert exact partitioning.
            tok = "sess-" + body.get("Token", "")
            return self._json({"Id": "s", "EntityId": "e", "Token": tok,
                               "AccessToken": "", "TTL": 3600, "Locality": "EU",
                               "Server": f"http://127.0.0.1:{PORT}", "UserName": "stub"})
        if path == "/__fault":
            faults.update(body); return self._json({"armed": faults})
        captured.append({"verb": "POST", "path": path, "body": body,
                         "auth": self.headers.get("Authorization")})
        if faults.get("429"):
            faults["429"] -= 1
            b = b'{"error":"rate limited"}'
            self.send_response(429); self.send_header("Retry-After", "1")
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(b))); self.end_headers()
            return self.wfile.write(b)
        if faults.get("401"):
            faults["401"] -= 1
            return self._json({"error": "session expired"}, 401)
        if path in SPECS:
            return self._json(response_for(path))
        return self._json({"error": f"no stub for {path}"}, 404)

    def do_GET(self):
        path = self.path.split("?")[0]
        if path == "/__captured":
            return self._json(captured)
        if path == "/__auth":
            return self._json(auth_calls)
        if path == "/__reset":
            captured.clear(); auth_calls.clear()
            faults.update({"429": 0, "401": 0})
            return self._json({"reset": True})
        captured.append({"verb": "GET", "path": path, "auth": self.headers.get("Authorization"),
                         "query": self.path.split("?", 1)[-1] if "?" in self.path else ""})
        if path in SPECS:
            return self._json(response_for(path))
        return self._json({"error": f"no stub for {path}"}, 404)

if __name__ == "__main__":
    HTTPServer(("127.0.0.1", PORT), H).serve_forever()
