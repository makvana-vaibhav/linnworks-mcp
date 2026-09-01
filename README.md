# LinnworksMcp Server (.NET 10)

An official Model Context Protocol (MCP) server for Linnworks e-commerce management built with .NET 10 (C#) and the official `ModelContextProtocol.AspNetCore` SDK.

---

## Overview

`LinnworksMcp` exposes Linnworks API capabilities as typed, standard Model Context Protocol (MCP) tools. Any AI client (such as Claude Code, Cursor, Copilot, or an agent backend like `rishvi-agent`) can connect to `LinnworksMcp` to manage inventory, stock levels, orders, channel listings, customers, shipping, purchase orders, and returns.

```
[ AI Client / Chatbot ] ── (MCP Protocol: stdio / Streamable HTTP) ──► [ LinnworksMcp Server ] ──► [ Linnworks REST API ]
```

---

## Key Features

- **Multi-Tenant / Per-User Isolation**: Caches user authorization sessions using Linnworks response `TTL` and refreshes proactively 60 seconds before expiration.
- **Region-Aware Base Routing**: Dynamic routing based on `session.Server` returned by `AuthorizeByApplication` (EU/US/AS region routing).
- **Dual Transport Support**:
  - `stdio`: For local execution (e.g. MCP Inspector, desktop clients launching as a subprocess).
  - `Streamable HTTP`: For remote network clients over standard HTTP endpoints with API key security.
- **Polly Resilience**: Automatic retries with exponential backoff and jitter for `429 Too Many Requests` and `5xx` server errors.
- **Rate Limiting**: Endpoint-scoped rate limiting to protect upstream Linnworks quotas.
- **Structured Observability**: End-to-end request tracing via ambient `CorrelationId` and `ToolMetrics`.

---

## Quick Start

### 1. Build the Solution

```bash
dotnet build LinnworksMcp.slnx
```

### 2. Run in Stdio Transport (Local Dev)

```bash
dotnet run --project src/LinnworksMcp -- --stdio
```

### 3. Run in Streamable HTTP Transport (Network Server)

```bash
dotnet run --project src/LinnworksMcp
```

The server starts on `http://localhost:5000`. Endpoint endpoints available:
- `POST /mcp` — MCP Protocol endpoint (protected by `X-Api-Key` header).
- `GET /health` — Liveness health check.
- `GET /ready` — Readiness check (verifies configuration and Linnworks connectivity).

---

## Tool Reference

| Module | MCP Tool Name | Read/Write | Description |
|---|---|---|---|
| **Inventory** | `get_inventory_items` | ReadOnly | Paginated inventory browse & SKU keyword filter |
| | `get_inventory_item_by_id` | ReadOnly | Full details for a single item by StockItemId (UUID) |
| | `get_low_stock_items` | ReadOnly | Items at or below minimum stock threshold |
| **Stock** | `get_stock_levels` | ReadOnly | Per-location stock levels for specific items |
| | `update_stock_levels` | Destructive | MUTATES DATA. Sets absolute stock level for a SKU at a location |
| **Orders** | `get_open_orders` | ReadOnly | List open orders for a warehouse location |
| | `get_order_by_id` | ReadOnly | Detailed view of orders by UUIDs |
| | `get_unfulfilled_orders` | ReadOnly | Open orders enriched with line items and shipping info |
| **Locations** | `get_locations` | ReadOnly | List all warehouse locations with names and UUIDs |
| | `get_location_by_id` | ReadOnly | Detailed view of a warehouse location |
| | `get_stock_by_location` | ReadOnly | Item stock levels filtered to a single location |
| **Listings** | `get_listings` | ReadOnly | Channel listings for a SKU |
| | `get_listing_by_id` | ReadOnly | Item channel listing details |
| | `get_channel_listing_errors` | ReadOnly | Channel listing sync error log |
| **Customers** | `search_customers` | ReadOnly | Search customers by name or email |
| | `get_customer_by_id` | ReadOnly | Detailed customer record |
| **Shipping** | `get_shipping_services` | ReadOnly | Available shipping services & postal providers |
| | `get_order_shipping_info` | ReadOnly | Tracking and shipment details for an order |
| **Purchase Orders** | `get_purchase_orders` | ReadOnly | List purchase orders by status/supplier |
| | `create_purchase_order` | Destructive | MUTATES DATA. Drafts a new purchase order |
| **Returns** | `get_returns` | ReadOnly | Search return requests / RMAs |
| | `create_return` | Destructive | MUTATES DATA. Creates a return request |

---

## Configuration

Environment variables (or `appsettings.json`):

```env
LINNWORKS_MCP_TRANSPORT=http
PORT=5000
Linnworks__AuthUrl=https://api.linnworks.net/api/Auth/AuthorizeByApplication
McpAuth__ApiKey=dev-secret-key-change-in-production
```

---

## Running Unit Tests

```bash
dotnet test LinnworksMcp.slnx
```

All Linnworks API HTTP calls and authentication flows are fully mocked in `LinnworksMcp.Tests`.
# linnworks-mcp
