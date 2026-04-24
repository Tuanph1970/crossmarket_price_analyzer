# CrossMarket Price Analyzer — User Guide

## Overview

CrossMarket Price Analyzer helps you find U.S. → Vietnam cross-border trade opportunities. It scrapes prices from U.S. retail sources (Amazon, Walmart, cigarpage.com), matches them against Vietnamese e-commerce platforms (Shopee, Lazada, Tiki), calculates landed costs, and scores opportunities by profit margin.

---

## Getting Started

### 1. Start the Application

```bash
# Start all services (database, cache, queue, APIs, frontend)
docker compose up -d

# Verify all containers are healthy
docker compose ps
```

Open your browser at **http://localhost:3003** (or **http://localhost:8081** via the API Gateway).

### 2. Create an Account

On first visit you will see the **Sign in** screen.

1. Click **Create an account**
2. Enter your full name, email, and a password (min 8 characters)
3. Click **Register** — you are logged in automatically

> You can also sign in with an existing account at any time.

---

## Features

### Dashboard

The main screen shows all scored trade opportunities in real time.

- **Opportunity cards** display the U.S. product, its matched Vietnamese equivalent, the price difference, and the composite profit score
- **Filters** (top bar) let you narrow by category, minimum score, or price range
- **Live updates** — new opportunities appear automatically via WebSocket without refreshing

---

### Quick Lookup

**Use this to instantly analyze any single product URL.**

1. Navigate to **Quick Lookup** in the left sidebar
2. Paste a product URL from a supported source:
   - `https://www.amazon.com/...`
   - `https://www.walmart.com/...`
   - `https://www.cigarpage.com/...`
   - `https://shopee.vn/...`
3. Click **Analyze**
4. Wait 15–60 seconds while the system:
   - Scrapes the product price
   - Searches for matching Vietnamese products
   - Calculates the landed cost and profit score
5. Results show:
   - **Scraped Product** — name, price (USD), exchange rate (VND/USD)
   - **Vietnam Matches** — matched VN products with confidence scores
   - **Opportunity Score** — composite profit potential rating

> **cigarpage.com** URLs are routed through FlareSolverr to bypass Cloudflare protection. Expect ~30 s response time for those.

---

### Compare

Compare two specific products side by side.

1. Go to **Compare** in the sidebar
2. Enter a U.S. product URL and a Vietnamese product URL
3. The system calculates the price delta, estimated import duties, shipping cost, and net margin

---

### Categories

Browse all scraped products grouped by product category. Use the category tree on the left to filter, and the sort controls to order by price, score, or date added.

---

### Price History

View the price trend for any tracked product over time.

1. Search for a product by name in the search bar (top of page)
2. Click the product to open its detail view
3. The **Price History** tab shows a line graph of daily price snapshots in both USD and VND

---

### Watchlist

Save products you want to monitor continuously.

1. From any opportunity card or product detail, click the **bookmark icon**
2. The product is added to your Watchlist
3. Go to **Watchlist** in the sidebar to view all saved items
4. Remove items with the **trash icon**

> Watchlist is private to your account and persists across sessions.

---

### Alerts

Set price threshold alerts so you are notified when a product's opportunity score or price crosses your target.

#### Create an Alert

1. Go to **Alerts** in the sidebar
2. Click **New Alert**
3. Fill in:
   - **Product** — search for the product to watch
   - **Metric** — score threshold, price drop, or margin change
   - **Threshold** — numeric value to trigger the alert
   - **Channel** — Email or Telegram
4. Click **Save**

#### Telegram Setup

To receive alerts via Telegram:
1. Search for **@CrossMarketBot** on Telegram and start a chat
2. Copy your **Chat ID** from the bot's welcome message
3. Paste it in **Settings → Telegram Chat ID**

#### Email Setup

Enter your email address in **Settings → Notification Email**. Alerts are delivered via SendGrid.

---

### Settings

| Setting | Description |
|---|---|
| **Display Language** | English / Vietnamese (EN / VI toggle in the top bar) |
| **Notification Email** | Email address for alert delivery |
| **Telegram Chat ID** | Telegram chat for instant alert notifications |
| **Exchange Rate Source** | Automatic (updated daily from public API) |

---

## How Scraping Works

| Source | Method | Notes |
|---|---|---|
| Amazon | Playwright (headless Chrome) | Anti-bot stealth headers |
| Walmart | Playwright (headless Chrome) | Anti-bot stealth headers |
| cigarpage.com | **FlareSolverr** → search page parse | Bypasses Cloudflare; uses search listing HTML |
| Shopee VN | HTTP API client | Official Shopee mobile API |
| Lazada VN | HTTP API client | Official Lazada mobile API |
| Tiki VN | Playwright (headless Chrome) | DOM scrape |

Scheduled background jobs run every few hours to keep prices fresh. You can trigger an on-demand scrape for a single product via **Quick Lookup**.

---

## Architecture Overview

```
Browser (React SPA :3003)
    ↓
YARP Gateway (:8080 / :8081)
    ↓
┌─────────────┬──────────────┬──────────────┬──────────────┬──────────────┐
│ ProductSvc  │ MatchingSvc  │ ScoringSvc   │ Notification │  AuthSvc     │
│   :5001     │   :5002      │   :5003      │    :5004     │   :5005      │
└─────────────┴──────────────┴──────────────┴──────────────┴──────────────┘
        ↓ RabbitMQ events            ↓ WebSocket
  MySQL · Redis · FlareSolverr (:8191)
```

---

## Running Tests

```bash
# Unit tests only (fast, no Docker required)
dotnet test CrossMarketAnalyzer.sln --filter "FullyQualifiedName~UnitTests"

# All tests including integration (requires full Docker stack)
dotnet test CrossMarketAnalyzer.sln

# Frontend unit tests
cd src/Apps/CMA.WebApp && npm run test

# Frontend e2e tests (requires app running on :5173 via npm run dev)
cd src/Apps/CMA.WebApp && npx playwright test --project=chromium
```

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| Quick Lookup returns "Failed to scrape" on cigarpage.com | FlareSolverr container not running | `docker compose up -d flaresolverr` |
| Quick Lookup very slow (>60 s) | FlareSolverr solving Cloudflare challenge | Normal — wait up to 90 s |
| No VN matches returned | Vietnamese product DB is empty | Wait for scheduled scraping jobs |
| Dashboard shows no opportunities | Scoring service not running | `docker compose up -d scoring-api` |
| WebSocket disconnects | Scoring service restarted | Page auto-reconnects within 5 s |
| Login fails | AuthService unhealthy | `docker compose restart auth-api` |
| Exchange rate shows 0 | External rate API unreachable | Check internet connectivity from container |

---

## Monitoring (optional)

Start with the observability stack:

```bash
docker compose --profile observability up -d
```

- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3001 (admin / admin) — pre-built *CMA Services Overview* dashboard

---

## API Reference

All endpoints are accessible via the Gateway at `http://localhost:8081`.

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/auth/register` | Register new user |
| `POST` | `/api/auth/login` | Login, returns JWT |
| `POST` | `/api/auth/refresh` | Refresh access token |
| `POST` | `/api/products/quick-lookup` | Scrape + match a URL on demand |
| `GET` | `/api/opportunities` | List scored opportunities |
| `GET` | `/api/products` | List all products |
| `GET` | `/api/matches` | List US↔VN product matches |
| `GET` | `/api/scores` | List opportunity scores |
| `GET/POST/DELETE` | `/api/watchlist` | Manage watchlist |
| `GET/POST/PUT/DELETE` | `/api/alerts/thresholds` | Manage alert thresholds |
| `WS` | `/ws/opportunities` | Real-time score updates |

All protected endpoints require the header:
```
Authorization: Bearer <access_token>
```

### Quick Lookup — Example

```bash
# Register
curl -X POST http://localhost:8081/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"MyPass123!","fullName":"Your Name"}'

# Quick Lookup (use the accessToken from register/login response)
curl -X POST http://localhost:8081/api/products/quick-lookup \
  -H "Content-Type: application/json" \
  -d '{"url":"https://www.cigarpage.com/arturo-fuente-hemingway-best-seller.html"}'
```

Expected response:
```json
{
  "scrapedProduct": {
    "name": "Arturo Fuente Hemingway",
    "price": 50.60,
    "currency": "USD",
    "sourceUrl": "https://www.cigarpage.com/arturo-fuente-hemingway-best-seller.html"
  },
  "vnMatches": [],
  "scores": [],
  "exchangeRate": 26189.99,
  "lookedUpAt": "2026-04-24T03:36:02Z"
}
```
