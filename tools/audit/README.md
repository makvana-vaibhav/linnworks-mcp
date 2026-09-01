# Contract audit harness

Verifies this server against Linnworks' published OpenAPI definitions, so a wrong endpoint
path, a renamed field or a mistyped property is caught here rather than in production.

Every endpoint's spec is cached under `spec/` (fetched from
`https://apidocs.linnworks.net/reference/<slug>.md`); `endpoints.json` maps path -> slug.

## Static audit — code contracts vs the spec

    python3 parse_cs.py    # extract request/response contracts from the C# source
    python3 audit.py       # diff them against the OpenAPI schemas

Flags wrong HTTP verbs, misspelled or miscased JSON fields, missing required request fields,
type mismatches (the class of bug that made `GeneralInfo.Status` fail to deserialize), and
array-vs-object response shape errors.

## Dynamic audits — the running server

`specstub.py` is a stub Linnworks whose responses are generated **from the real schemas**, so
every field carries the type Linnworks declares. It also supports fault injection.

    python3 specstub.py &                    # stub on :5199

    # separate shell, with the server pointed at the stub:
    #   McpAuth__ApiKey=dev-local-key \
    #   Linnworks__AuthUrl=http://127.0.0.1:5199/api/Auth/AuthorizeByApplication \
    #   ASPNETCORE_URLS=http://127.0.0.1:5177 dotnet run --project ../../src/LinnworksMcp

    python3 toolaudit.py    # every registered tool: request shape + response mapping
    python3 infraaudit.py   # validation, client auth, session cache, tenant isolation, retries
    python3 stdio_test.py   # stdio transport: handshake, discovery, invocation, shutdown

Stub control endpoints: `/__captured` (requests received), `/__auth` (authorize calls),
`/__reset`, and `POST /__fault {"429": n, "401": n}` to arm transient failures.

## What this does not cover

The stub proves wire format and mapping, not your account's real data. Re-run at least one
live read after deploying. Endpoints for the unregistered modules (Listings, Customers,
Shipping, PurchaseOrders, Returns) are deliberately absent — see the notes in each service.
