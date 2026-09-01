# LinnworksMcp — Complete Setup & Connector Guide

This guide explains how to deploy `LinnworksMcp` and securely connect it to **Claude Desktop**, **Claude Web (Browser)**, or a custom **SaaS Web Application**.

---

## 🔒 Security Principle: Credentials Never Live in Chat

Credentials (`ApplicationId`, `ApplicationSecret`, `Token`) are **NEVER typed into the chat prompt**. Depending on how you run the server, they live safely in:

1. **Local Desktop File**: `claude_desktop_config.json` (for Claude Desktop).
2. **Server Environment Variables**: Docker / Cloud env variables (for Claude Web).
3. **Encrypted User Settings UI**: User Integration Database (for multi-tenant SaaS apps).

---

## 🖥️ 1. Local Setup with Claude Desktop (Stdio Mode)

Runs locally on your computer as a background process (`stdio` mode).

### Step 1: Build the Project
```bash
cd LinnworksMcp
dotnet publish src/LinnworksMcp/LinnworksMcp.csproj -c Release -o ./publish
```

### Step 2: Edit `claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

Add the server entry:
```json
{
  "mcpServers": {
    "linnworks": {
      "command": "dotnet",
      "args": [
        "/absolute/path/to/LinnworksMcp/publish/LinnworksMcp.dll",
        "--stdio"
      ],
      "env": {
        "Linnworks__AuthUrl": "https://api.linnworks.net/api/Auth/AuthorizeByApplication",
        "Linnworks__ApplicationId": "YOUR-LINNWORKS-APP-ID",
        "Linnworks__ApplicationSecret": "YOUR-LINNWORKS-APP-SECRET",
        "Linnworks__Token": "YOUR-LINNWORKS-USER-TOKEN"
      }
    }
  }
}
```

### Step 3: Restart Claude Desktop
Quit and re-open Claude Desktop. Look for the 🔨 hammer icon showing connected tools.

---

## 🌐 2. Web Setup with Claude Browser / Remote Connectors (HTTP Mode)

Connects **Claude Web (claude.ai)** to your hosted server over the internet.

### Step 1: Deploy / Expose Public HTTPS URL
- **Cloud Deployment**: Deploy container using `Dockerfile` or `docker-compose.yml` to AWS, Render, DigitalOcean, etc.
- **Local Tunnel**: Expose port 5000 via Cloudflare Tunnel:
  ```bash
  cloudflared tunnel --url http://localhost:5000
  ```
  *Example URL*: `https://linnworks-mcp.yourdomain.com`

### Step 2: Set Server Environment Variables
On your server or `.env`:
```env
LINNWORKS_MCP_TRANSPORT=http
PORT=5000
McpAuth__ApiKey=your-secret-mcp-key

Linnworks__AuthUrl=https://api.linnworks.net/api/Auth/AuthorizeByApplication
Linnworks__ApplicationId=your-linnworks-app-id
Linnworks__ApplicationSecret=your-linnworks-app-secret
Linnworks__Token=your-linnworks-user-token
```

### Step 3: Connect in Claude Browser (claude.ai)
1. Go to **Settings ➔ Integrations / Connectors** in claude.ai.
2. Add Custom MCP Server:
   - **URL**: `https://linnworks-mcp.yourdomain.com/mcp`
   - **Header Name**: `X-Api-Key`
   - **Header Value**: `your-secret-mcp-key`
3. Save and start chatting naturally.

---

## 🏢 3. Multi-Tenant SaaS Web App Integration

For hosting an AI chatbot where multiple different users connect their own Linnworks accounts:

1. End users enter their 4 credentials in your Web App's **Settings UI**.
2. Your server encrypts and stores them in your database.
3. When the user sends a message, your app backend forwards credentials to `LinnworksMcp` via HTTP headers:
   ```http
   POST /mcp HTTP/1.1
   Host: mcp.yourdomain.com
   X-Api-Key: your-secret-mcp-key
   X-Linnworks-UserId: user-123
   X-Linnworks-Token: token-value
   X-Linnworks-ApplicationId: app-id
   X-Linnworks-ApplicationSecret: app-secret
   ```

---

## 🧰 4. Quick Tool Reference

| Category | Tools | Description |
|---|---|---|
| **Inventory** | `get_inventory_items`, `get_inventory_item_by_id`, `get_low_stock_items` | Browse stock, search SKU, shortage report |
| **Stock** | `get_stock_levels`, `update_stock_levels` (destructive) | Query stock per location & update stock levels |
| **Orders** | `get_open_orders`, `get_order_by_id`, `get_unfulfilled_orders` | Open orders, unfulfilled items & details |
| **Locations** | `get_locations`, `get_location_by_id`, `get_stock_by_location` | Warehouse locations & location stock |
| **Listings** | `get_listings`, `get_listing_by_id`, `get_channel_listing_errors` | Channel listings & sync error logs |
| **Customers** | `search_customers`, `get_customer_by_id` | Customer lookup & details |
| **Shipping** | `get_shipping_services`, `get_order_shipping_info` | Courier services & order tracking |
| **Purchase Orders** | `get_purchase_orders`, `create_purchase_order` (destructive) | List POs & draft new supplier POs |
| **Returns** | `get_returns`, `create_return` (destructive) | Search RMAs & create return requests |
