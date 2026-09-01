# Server Deployment & Hosting Guide — `linnworks-mcp.rishvi.app`

This guide provides step-by-step commands to deploy **`LinnworksMcp`** on a Linux server under `/var/apps/linnworks-mcp` using **Docker Compose**, **Nginx**, and **Certbot SSL**.

---

## 📋 Dual-Mode Capability

Your hosted server at `https://linnworks-mcp.rishvi.app` supports **both usage modes simultaneously**:

1. **Mode 1 (Claude Browser / Direct Connectors)**: When called from Claude Web (claude.ai) with just `X-Api-Key`, the server uses the default server Linnworks credentials configured in `.env`.
2. **Mode 2 (Multi-Tenant SaaS / Personal Chatbot Backend)**: When called from a web chatbot backend like `rishvi-agent`, your app passes per-user Linnworks credentials via HTTP headers (`X-Linnworks-Application-Id`, `X-Linnworks-Token`, etc.), which override the server defaults per request.

---

## 🚀 Step 1: Clone Repository & Create Directory

Run on your Linux server:

```bash
# Create target apps directory
sudo mkdir -p /var/apps
sudo chown -R $USER:$USER /var/apps

# Clone repository into /var/apps/linnworks-mcp
cd /var/apps
git clone https://github.com/makvana-vaibhav/linnworks-mcp.git linnworks-mcp
cd /var/apps/linnworks-mcp
```

---

## ⚙️ Step 2: Create Environment Configuration (`.env`)

Create the `.env` file in `/var/apps/linnworks-mcp`:

```bash
cat << 'EOF' > /var/apps/linnworks-mcp/.env
LINNWORKS_MCP_TRANSPORT=http
PORT=5000
ASPNETCORE_ENVIRONMENT=Production

# Generate/set a secret API key for MCP client authorization
McpAuth__ApiKey=prod-secret-mcp-key-change-this

# Base Linnworks Auth URL
Linnworks__AuthUrl=https://api.linnworks.net/api/Auth/AuthorizeByApplication

# Default Linnworks Credentials (Used for Claude Web Connector mode)
Linnworks__Stdio__ApplicationId=your-linnworks-app-id
Linnworks__Stdio__ApplicationSecret=your-linnworks-app-secret
Linnworks__Stdio__Token=your-linnworks-user-token
EOF
```

---

## 🐳 Step 3: Build & Start Docker Container

```bash
cd /var/apps/linnworks-mcp

# Build and start container in detached mode
docker compose up -d --build

# Verify container status
docker compose ps

# Test internal health check
curl http://127.0.0.1:5000/health
```

Expected response: `{"status":"Healthy"}`.

---

## 🌐 Step 4: Configure Nginx Reverse Proxy

Create Nginx site configuration at `/etc/nginx/sites-available/linnworks-mcp.rishvi.app`:

```bash
sudo cat << 'EOF' | sudo tee /etc/nginx/sites-available/linnworks-mcp.rishvi.app
server
{
    server_name linnworks-mcp.rishvi.app;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Disable buffering for streaming MCP responses
        proxy_buffering off;
        proxy_read_timeout 300s;
    }

    listen 443 ssl; # managed by Certbot
    ssl_certificate /etc/letsencrypt/live/rishvi.app/fullchain.pem; # managed by Certbot
    ssl_certificate_key /etc/letsencrypt/live/rishvi.app/privkey.pem; # managed by Certbot
    include /etc/letsencrypt/options-ssl-nginx.conf; # managed by Certbot
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem; # managed by Certbot
}

server {
    if ($host = linnworks-mcp.rishvi.app) {
        return 301 https://$host$request_uri;
    } # managed by Certbot

    server_name linnworks-mcp.rishvi.app;
    listen 80;
    return 404; # managed by Certbot
}
EOF
```

---

## 🔗 Step 5: Enable Nginx Site & Reload

```bash
# Enable the site configuration
sudo ln -sf /etc/nginx/sites-available/linnworks-mcp.rishvi.app /etc/nginx/sites-enabled/

# Test Nginx syntax
sudo nginx -t

# Reload Nginx
sudo systemctl reload nginx
```

---

## ✅ Step 6: Verify Deployment

Run health checks:

```bash
# Check HTTPS Health Check
curl -i https://linnworks-mcp.rishvi.app/health

# Check HTTPS Readiness Check
curl -i https://linnworks-mcp.rishvi.app/ready
```
