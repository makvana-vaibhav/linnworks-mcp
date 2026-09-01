# LinnworksMcp Master Documentation (.NET 10)

Welcome to **LinnworksMcp**, an official Model Context Protocol (MCP) server for Linnworks e-commerce management built with **.NET 10 (C#)** and the official `ModelContextProtocol.AspNetCore` SDK.

This master document explains **everything** about `LinnworksMcp` in a beginner-friendly, step-by-step manner: what MCP is, the complete project structure, how client and Linnworks authentication works, how regional routing and resilience are handled, and how each API service operates.

---

## 📚 Table of Contents

1. [High-Level Overview & Concepts](#1-high-level-overview--concepts)
2. [What is Model Context Protocol (MCP)?](#2-what-is-model-context-protocol-mcp)
3. [Architecture & Request Flow](#3-architecture--request-flow)
4. [Folder & File Structure Deep Dive](#4-folder--file-structure-deep-dive)
5. [Authentication System (Step-by-Step)](#5-authentication-system-step-by-step)
   - [Level 1: Client to MCP Server Security](#level-1-client-to-mcp-server-security)
   - [Level 2: MCP Server to Linnworks API Security](#level-2-mcp-server-to-linnworks-api-security)
6. [Linnworks Connection & Resilience Mechanics](#6-linnworks-connection--resilience-mechanics)
   - [Dynamic Regional Routing](#dynamic-regional-routing)
   - [Session Caching & 60s Early Refresh](#session-caching--60s-early-refresh)
   - [Single-Flight Lock (Concurrency Protection)](#single-flight-lock-concurrency-protection)
   - [Polly Resilience & 401 Self-Healing](#polly-resilience--401-self-healing)
7. [API Services & Tool Reference](#7-api-services--tool-reference)
   - [Registered Core Tools](#registered-core-tools)
   - [Staged / Unregistered Modules](#staged--unregistered-modules)
8. [Error Handling & Observability](#8-error-handling--observability)
9. [Configuration & Environment Variables](#9-configuration--environment-variables)
10. [Transports & How to Run](#10-transports--how-to-run)
11. [Docker Deployment](#11-docker-deployment)

---

## 1. High-Level Overview & Concepts

**Linnworks** is an enterprise e-commerce order and inventory management system. It manages stock across multiple warehouses, listing channels (Amazon, eBay, Shopify, etc.), orders, shipping labels, and purchase orders.

**LinnworksMcp** acts as a smart bridge between AI Assistants (like Claude, Cursor, Copilot, or custom agents like `rishvi-agent`) and Linnworks. Instead of writing custom API integration code for every AI agent, `LinnworksMcp` exposes Linnworks functionalities as standardized **MCP Tools**.

---

## 2. What is Model Context Protocol (MCP)?

The **Model Context Protocol (MCP)** is an open standard created by Anthropic that allows AI models to safely interact with external data sources and execution environments.

### Key Concepts:
- **MCP Host / Client**: The AI application (e.g., Claude Desktop, Cursor IDE, or `rishvi-agent`).
- **MCP Server**: This project (`LinnworksMcp`), which exposes functions (called **Tools**) that the AI can call.
- **MCP Tools**: Typed functions defined by the server (e.g. `get_open_orders`, `update_stock_levels`). The AI inspects tool names and parameters, decides which tool to run, and sends a JSON-RPC request to the MCP server.

---

## 3. Architecture & Request Flow

Below is the end-to-end communication flow when an AI agent requests data from Linnworks through `LinnworksMcp`:

```
┌───────────────────────────┐
│   AI Client / Agent       │  (e.g., Claude, Cursor, rishvi-agent)
└─────────────┬─────────────┘
              │ 1. Sends Tool Request (JSON-RPC) over Stdio or HTTP (/mcp)
              ▼
┌───────────────────────────┐
│   LinnworksMcp Server     │
├───────────────────────────┤
│ • McpAccessMiddleware     │ ➔ 2. Validates MCP Client API Key (X-Api-Key)
│ • ToolAuthorizer          │ ➔ 3. Checks tool permissions & destructive policies
│ • CredentialProvider      │ ➔ 4. Extracts tenant Linnworks credentials (X-Linnworks-*)
│ • LinnworksAuthManager    │ ➔ 5. Retrieves or refreshes cached session token & region
│ • LinnworksClient         │ ➔ 6. Executes HTTP request with Polly retries & rate limits
└─────────────┬─────────────┘
              │ 7. Authenticated REST Request (Authorization: <token>)
              ▼
┌───────────────────────────┐
│   Linnworks REST API      │  (e.g., https://eu-ext.linnworks.net/api/Orders/GetOpenOrders)
└───────────────────────────┘
```

---

## 4. Folder & File Structure Deep Dive

Here is the exact layout of the codebase and the purpose of every file:

```
LinnworksMcp/
├── Program.cs                      # Application entry point & pipeline setup
├── LinnworksMcp.csproj             # .NET 10 project file & package dependencies
├── appsettings.json                # Server configuration defaults
├── Dockerfile                      # Container build definition
├── docker-compose.yml              # Local container orchestration
├── docs/                           # Technical documentation & guides
│   ├── API_COVERAGE.md             # Linnworks API endpoints coverage status
│   ├── CONNECTOR_SETUP_GUIDE.md    # Guide for connecting Claude / Agents
│   └── SERVER_HOSTING_GUIDE.md     # Production deployment instructions
└── src/LinnworksMcp/
    ├── Application/                # Business & Domain Services
    │   ├── Customers/
    │   │   └── CustomerService.cs  # Customer search & detail retrieval
    │   ├── Inventory/
    │   │   └── InventoryService.cs # Item listing, SKU lookups, low stock
    │   ├── Listings/
    │   │   └── ListingService.cs   # Channel listings & sync error logs
    │   ├── Locations/
    │   │   └── LocationService.cs  # Warehouse location lookups & resolution
    │   ├── Orders/
    │   │   └── OrderService.cs     # Open orders, order details, unfulfilled orders
    │   ├── PurchaseOrders/
    │   │   └── PurchaseOrderService.cs # PO creation & listing
    │   ├── Returns/
    │   │   └── ReturnService.cs    # Returns & RMA handling
    │   ├── Shipping/
    │   │   └── ShippingService.cs  # Shipping services & order tracking info
    │   └── Stock/
    │       └── StockService.cs     # Stock level lookups & stock level updates
    ├── Infrastructure/             # Technical Cross-Cutting Concerns
    │   ├── Auth/
    │   │   ├── ApiKeyAuthenticationHandler.cs # Extracts X-Api-Key / Bearer header
    │   │   ├── McpAccessMiddleware.cs          # HTTP gatekeeper on /mcp
    │   │   └── ToolAuthorization.cs           # Policy engine for tool execution & mutation
    │   ├── Linnworks/
    │   │   ├── EndpointRateLimiter.cs        # Client-side rate limiter per endpoint
    │   │   ├── LinnworksApiException.cs      # Custom exception for domain & HTTP errors
    │   │   ├── LinnworksAuthManager.cs       # Singleton session cache & auth logic
    │   │   ├── LinnworksClient.cs            # Linnworks REST API HTTP wrapper
    │   │   ├── LinnworksCredentialProvider.cs# Hybrid header/config credential provider
    │   │   ├── LinnworksCredentials.cs       # Credentials model & fingerprinting
    │   │   ├── LinnworksOptions.cs           # Configuration settings model
    │   │   └── LinnworksSession.cs           # Linnworks session response DTO
    │   ├── Observability/
    │   │   ├── CorrelationId.cs              # Ambient request tracing context
    │   │   ├── LinnworksReadinessCheck.cs    # ASP.NET Core Health Check for Linnworks
    │   │   └── ToolMetrics.cs                # In-memory metrics tracking (calls, latency, retries)
    │   └── ServiceCollectionExtensions.cs    # DI container setup & Polly resilience policies
    ├── Mcp/                        # Protocol Layer
    │   ├── McpServerSetup.cs             # MCP Server builder & tool registration
    │   └── Tools/                        # Exposed MCP Tool Handlers
    │       ├── CustomerTools.cs
    │       ├── InventoryTools.cs
    │       ├── ListingTools.cs
    │       ├── LocationTools.cs
    │       ├── OrderTools.cs
    │       ├── PurchaseOrderTools.cs
    │       ├── ReturnTools.cs
    │       ├── ShippingTools.cs
    │       └── StockTools.cs
    ├── Models/                     # Shared Domain Data Models
    │   ├── InventoryItem.cs
    │   ├── Location.cs
    │   ├── Order.cs
    │   ├── PagedResult.cs
    │   └── StockLevel.cs
    └── Utils/                      # Utility Helpers
        └── ErrorHandling.cs        # ToolExecution wrapper & validation helpers
```

---

## 5. Authentication System (Step-by-Step)

The authentication system operates in **two distinct layers**:

### Level 1: Client to MCP Server Security

When an AI client (e.g. Claude Desktop or `rishvi-agent`) connects to `LinnworksMcp` over HTTP:

1. **API Key Verification (`McpAccessMiddleware`)**:
   - The client supplies an API key via header `X-Api-Key: <key>` or `Authorization: Bearer <key>`.
   - `McpAccessMiddleware` intercepts requests to `/mcp`. If `McpAuth:ApiKey` is set on the server, unauthorized requests receive HTTP `401 Unauthorized`.
2. **Anonymous Discovery Exemption**:
   - MCP discovery methods (`initialize`, `ping`, `tools/list`) do not expose Linnworks data.
   - If `AllowAnonymousDiscovery` is set to `true` (default), AI clients can explore available tools before authenticating. Tool execution still requires authorization.
3. **Destructive Tool Guardrails (`ToolAuthorization.cs`)**:
   - Tools that modify data (e.g. `update_stock_levels`, `create_purchase_order`) are marked as **Destructive**.
   - If `McpAuth:AllowDestructiveTools` is set to `false`, destructive tool execution is denied globally.
   - If `DestructiveToolApiKeys` is configured, only whitelisted API keys can execute mutating operations.

### Level 2: MCP Server to Linnworks API Security

To call the Linnworks API, `LinnworksMcp` needs Linnworks credentials: `ApplicationId`, `ApplicationSecret`, `Token`, and `UserId`.

1. **Credential Resolution (`HeaderLinnworksCredentialProvider`)**:
   - **Multi-Tenant SaaS Mode**: The client passes user credentials in HTTP headers:
     - `X-Linnworks-User-Id`
     - `X-Linnworks-Application-Id`
     - `X-Linnworks-Application-Secret`
     - `X-Linnworks-Token`
   - **Single-Tenant / Fallback Mode**: If headers are absent, credentials are read from environment variables or `appsettings.json` (`Linnworks__Stdio__*`).
2. **Linnworks Authorization Request (`LinnworksAuthManager`)**:
   - To authenticate, the server posts to `POST https://api.linnworks.net/api/Auth/AuthorizeByApplication`.
   - Linnworks responds with:
     - `Token`: A temporary session token.
     - `Server`: The dynamic regional API endpoint (e.g. `eu-ext.linnworks.net`).
     - `Locality`: Region indicator (e.g., `EU`, `US`).
     - `Ttl`: Lifetime of the session token in seconds (typically 86,400s / 24 hours).

---

## 6. Linnworks Connection & Resilience Mechanics

### Dynamic Regional Routing
Linnworks operates regional data centers across Europe, North America, and Asia. When `AuthorizeByApplication` succeeds, Linnworks specifies the target region in `session.Server`. `LinnworksClient.BuildUri()` dynamically constructs every API URL using this regional server host instead of hardcoding a single URL.

### Session Caching & 60s Early Refresh
Re-authenticating on every tool call wastes time and risks hitting Linnworks authentication rate limits.
- `LinnworksAuthManager` is registered as a **Singleton** service.
- It caches sessions in a `ConcurrentDictionary` keyed by `UserId`.
- Before using a cached session, it checks the remaining lifetime. If less than **60 seconds** remain before expiration (`SessionRefreshBuffer`), the cached session is discarded and a fresh session is fetched.

### Single-Flight Lock (Concurrency Protection)
If multiple AI tool calls for the same tenant arrive simultaneously while a session is expired, sending multiple parallel `AuthorizeByApplication` requests would waste quota and trigger rate limits.
- `LinnworksAuthManager` uses a per-user `SemaphoreSlim` lock.
- Only **one** authorization call per user runs at a time. Other concurrent calls wait for the first to complete and then reuse the newly cached session.

### Credential Fingerprinting
If a client presents a `UserId` that is already in the cache but with different credentials (e.g. tenant credential rotation), `LinnworksAuthManager` detects the fingerprint mismatch, evicts the stale session immediately, and re-authenticates.

### Polly Resilience & 401 Self-Healing
All Linnworks HTTP requests are routed through a Polly resilience pipeline (`ServiceCollectionExtensions.cs`):
- **Exponential Backoff with Jitter**: Retries up to 3 times on `429 Too Many Requests` or `5xx Server Errors`.
- **`Retry-After` Header Respect**: If Linnworks specifies a `Retry-After` header during rate-limiting, Polly waits for the exact requested duration.
- **401 Unauthorized Self-Healing**: If Linnworks rejects a session token (e.g., due to server-side session revocation), `LinnworksClient` catches the `401`, invalidates the cached session, obtains a new session, and retries the request once transparently.

---

## 7. API Services & Tool Reference

### Registered Core Tools

The following tools are fully registered in `McpServerSetup.cs` and available for execution:

| Module | Tool Name | Mode | Description |
|---|---|---|---|
| **Inventory** | `get_inventory_items` | ReadOnly | Paginated inventory list with SKU keyword search & stock filters |
| | `get_inventory_item_by_id` | ReadOnly | Complete item details by `StockItemId` (UUID) |
| | `get_low_stock_items` | ReadOnly | Items where stock level is at or below minimum reorder point |
| **Stock** | `get_stock_levels` | ReadOnly | Per-location stock quantities for requested items |
| | `update_stock_levels` | **Destructive** | Set absolute stock levels for a SKU at a warehouse location |
| **Orders** | `get_open_orders` | ReadOnly | List open (unprocessed) orders for a location |
| | `get_order_by_id` | ReadOnly | Full details of orders by UUIDs (items, customer, totals, shipping) |
| | `get_unfulfilled_orders` | ReadOnly | Open orders enriched with line items and shipping info |
| **Locations** | `get_locations` | ReadOnly | List all warehouse locations with names and UUIDs |
| | `get_location_by_id` | ReadOnly | View detailed information for a specific warehouse location |
| | `get_stock_by_location` | ReadOnly | List stock quantities across items at a single location |

### Staged / Unregistered Modules

The following service modules exist in `src/LinnworksMcp/Application/` and tool handlers exist in `src/LinnworksMcp/Mcp/Tools/`:
- **Listings** (`ListingTools.cs`, `ListingService.cs`)
- **Customers** (`CustomerTools.cs`, `CustomerService.cs`)
- **Shipping** (`ShippingTools.cs`, `ShippingService.cs`)
- **Purchase Orders** (`PurchaseOrderTools.cs`, `PurchaseOrderService.cs`)
- **Returns** (`ReturnTools.cs`, `ReturnService.cs`)

> **Note on Staged Modules**: These tool types are implemented but temporarily commented out in `McpServerSetup.cs` until their request/response schemas are fully verified against official Linnworks REST documentation. To register any of these modules, uncomment `.WithTools<ModuleName>()` in `McpServerSetup.cs`.

---

## 8. Error Handling & Observability

### Unified Error Structure (`ToolExecution.RunAsync`)
All tool invocations run within `ToolExecution.RunAsync` (`Utils/ErrorHandling.cs`). If an exception occurs, it returns a clean JSON error response rather than crashing the process:

```json
{
  "error": true,
  "kind": "Authentication",
  "message": "Authentication failed — session may have expired or credentials are invalid.",
  "detail": "AuthorizeByApplication rejected the credentials [400]"
}
```

### Correlation IDs & Metrics
- **Correlation ID**: Every HTTP request receives an ambient correlation ID (`CorrelationIdMiddleware.cs`), included in all log output.
- **Tool Metrics (`ToolMetrics.cs`)**: Tracks total tool calls, success rates, throttled requests (HTTP 429), retries, and execution latencies in memory.

---

## 9. Configuration & Environment Variables

Configuration can be supplied via environment variables or `appsettings.json`:

```env
# Transport Mode: "http" (default) or "stdio"
LINNWORKS_MCP_TRANSPORT=http
PORT=5000

# Server HTTP Authentication
McpAuth__ApiKey=dev-secret-key-change-in-production
McpAuth__AllowDestructiveTools=true
McpAuth__AllowAnonymousDiscovery=true
McpAuth__RequireApiKey=true

# Server-Side Linnworks Fallback Credentials (Single-Tenant Mode)
Linnworks__Stdio__UserId=default-user
Linnworks__Stdio__ApplicationId=your-application-id
Linnworks__Stdio__ApplicationSecret=your-application-secret
Linnworks__Stdio__Token=your-application-token

# Linnworks Auth URL
Linnworks__AuthUrl=https://api.linnworks.net/api/Auth/AuthorizeByApplication
```

---

## 10. Transports & How to Run

`LinnworksMcp` supports two transport modes:

### Mode 1: Streamable HTTP Transport (Recommended for Web Servers & Agents)

Runs an ASP.NET Core web application listening on HTTP/HTTPS.

```bash
# Run HTTP transport (default port 5000)
dotnet run --project src/LinnworksMcp
```

Endpoints exposed:
- `POST /mcp` — MCP JSON-RPC protocol endpoint (protected by `X-Api-Key`).
- `GET /health` — Liveness check.
- `GET /ready` — Readiness check (verifies Linnworks options & connectivity).

### Mode 2: Stdio Transport (For Local Dev & Desktop Clients)

Runs as a CLI subprocess communicating via standard input/output (`stdin`/`stdout`).

```bash
# Run stdio transport
dotnet run --project src/LinnworksMcp -- --stdio
```

> **Important**: When running in `stdio` mode, application logs are automatically routed to `stderr` so they do not corrupt the JSON-RPC stream on `stdout`.

---

## 11. Docker Deployment

### Building & Running with Docker

```bash
# Build the Docker image
docker build -t linnworks-mcp .

# Run the container
docker run -d -p 5000:5000 \
  -e McpAuth__ApiKey=your-secure-api-key \
  --name linnworks-mcp-server linnworks-mcp
```

### Docker Compose

Run using `docker-compose`:

```bash
docker-compose up -d --build
```

---

## 🧪 Running Unit Tests

`LinnworksMcp` includes comprehensive unit tests in `tests/LinnworksMcp.Tests` with mocked HTTP callers and Linnworks sessions:

```bash
dotnet test LinnworksMcp.slnx
```

---

## 📄 License & Maintainers

Maintained by the **Rishvi Team**. Built on .NET 10 & official `ModelContextProtocol.AspNetCore` SDK.

