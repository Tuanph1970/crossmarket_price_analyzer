# CrossMarket Price Analyzer — Phase 3 (v1.5) Production Deployment Guide

> **Applies to:** v1.5 release (Week 15–22). Covers all Phase 3 backend services,
> WebSocket server, Excel export, HS code classifier, TariffService, and frontend.

---

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| .NET SDK | ≥ 9.0 | Required for local build |
| Docker | ≥ 24.0 | For containerised deployment |
| Docker Compose | ≥ 2.20 | `docker compose` plugin |
| MySQL | 8.0+ | Separate DB per service |
| Redis | 7.0+ | Required for caching and pub/sub |
| RabbitMQ | 3.12+ | Required for event bus |

---

## 1. Image Build

All services follow the same build pattern:

```bash
# ── Build scoring service (API + Worker) ────────────────────────────────────
dotnet publish src/Services/ScoringService/ScoringService.Api \
  -c Release -o ./publish/scoring-api --no-self-contained

docker build \
  --build-arg BUILD_CONFIG=Release \
  -f src/Services/ScoringService/ScoringService.Api/Dockerfile \
  -t crossmarket/scoring-service:1.5.0 \
  .

# ── Build scraping service (Worker only) ─────────────────────────────────────
dotnet publish src/Services/ScrapingService/ScrapingService.Worker \
  -c Release -o ./publish/scraping-worker --no-self-contained

docker build \
  -f src/Services/ScrapingService/ScrapingService.Worker/Dockerfile \
  -t crossmarket/scraping-service:1.5.0 \
  .
```

---

## 2. Docker Compose Configuration

### `deploy/docker-compose.prod.yml`

```yaml
version: '3.9'

services:
  # ── Infrastructure ─────────────────────────────────────────────────────────
  mysql:
    image: mysql:8.0
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
    volumes:
      - mysql_data:/var/lib/mysql
    command: --default-authentication-plugin=mysql_native_password

  redis:
    image: redis:7-alpine
    restart: unless-stopped
    volumes:
      - redis_data:/data

  rabbitmq:
    image: rabbitmq:3.12-management
    restart: unless-stopped
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER:-guest}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:-guest}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    ports:
      - "15672:15672"   # Management UI

  # ── Core Services ────────────────────────────────────────────────────────────
  product-service:
    image: crossmarket/product-service:1.5.0
    restart: unless-stopped
    depends_on: [mysql, redis, rabbitmq]
    environment:
      ConnectionStrings__DefaultConnection: ${DATABASE_URL}/cma_products
      ConnectionStrings__RedisConnection: ${REDIS_URL}
      ConnectionStrings__RabbitMqConnection: ${RABBITMQ_URL}
      ASPNETCORE_ENVIRONMENT: Production

  matching-service:
    image: crossmarket/matching-service:1.5.0
    restart: unless-stopped
    depends_on: [mysql, redis, rabbitmq]
    environment:
      ConnectionStrings__DefaultConnection: ${DATABASE_URL}/cma_matching
      ConnectionStrings__RedisConnection: ${REDIS_URL}
      ConnectionStrings__RabbitMqConnection: ${RABBITMQ_URL}
      ASPNETCORE_ENVIRONMENT: Production

  # ── Phase 3: Scoring Service ─────────────────────────────────────────────────
  scoring-service:
    image: crossmarket/scoring-service:1.5.0
    restart: unless-stopped
    depends_on: [mysql, redis, rabbitmq]
    environment:
      ConnectionStrings__DefaultConnection: ${DATABASE_URL}/cma_scoring
      ConnectionStrings__RedisConnection: ${REDIS_URL}
      ConnectionStrings__RabbitMqConnection: ${RABBITMQ_URL}
      # Phase 3: ProductService HTTP client for price history
      HttpClient__ProductService__BaseAddress: http://product-service:5001
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: "http://+:5003"
    ports:
      - "5003:5003"

  # ── Phase 3: Notification Service ───────────────────────────────────────────
  notification-service:
    image: crossmarket/notification-service:1.5.0
    restart: unless-stopped
    depends_on: [redis, rabbitmq]
    environment:
      ConnectionStrings__DefaultConnection: ${DATABASE_URL}/cma_notifications
      ConnectionStrings__RedisConnection: ${REDIS_URL}
      ConnectionStrings__RabbitMqConnection: ${RABBITMQ_URL}
      ASPNETCORE_ENVIRONMENT: Production

  # ── Phase 3: Scraping Worker ─────────────────────────────────────────────────
  scraping-worker:
    image: crossmarket/scraping-service:1.5.0
    restart: unless-stopped
    depends_on: [mysql, redis, rabbitmq]
    environment:
      ConnectionStrings__DefaultConnection: ${DATABASE_URL}/cma_scraping
      ConnectionStrings__RedisConnection: ${REDIS_URL}
      ConnectionStrings__RabbitMqConnection: ${RABBITMQ_URL}
      ASPNETCORE_ENVIRONMENT: Production
    # No HTTP port — worker only

  # ── API Gateway ──────────────────────────────────────────────────────────────
  gateway:
    image: crossmarket/gateway:1.5.0
    restart: unless-stopped
    depends_on: [scoring-service, matching-service, product-service]
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production

  # ── Frontend (static) ─────────────────────────────────────────────────────────
  frontend:
    image: crossmarket/frontend:1.5.0
    restart: unless-stopped
    ports:
      - "3000:3000"

volumes:
  mysql_data:
  redis_data:
  rabbitmq_data:
```

---

## 3. Environment Variables

Create `.env.prod` before deployment:

```bash
# ── Infrastructure ───────────────────────────────────────────────────────────
MYSQL_ROOT_PASSWORD=CHANGE_ME_secure_password
DATABASE_URL=mysql://root:CHANGE_ME_secure_password@mysql:3306
REDIS_URL=redis://redis:6379
RABBITMQ_URL=amqp://guest:guest@rabbitmq:5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=CHANGE_ME_secure_password

# ── ASP.NET Core ────────────────────────────────────────────────────────────────
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# ── Frontend ──────────────────────────────────────────────────────────────────
VITE_API_GATEWAY_URL=http://localhost:8080
```

---

## 4. Phase 3 — New Endpoints

### WebSocket Server (ScoringService)
```
ws://<host>:5003/ws/opportunities   # real-time score broadcasts
```
Subprotocol: `cma-v1` — client MUST send this during handshake.

**Broadcast types:**
| Type | Payload | When |
|---|---|---|
| `full_snapshot` | `OpportunitySnapshotDto` | On connect, after recalculate |
| `score_delta` | `ScoreDeltaDto` | On individual score change |
| `heartbeat` | `{ at }` | Every 60s (keep-alive) |
| `export_completed` | `{ recordCount, at }` | After Excel export finishes |

### REST Endpoints (Phase 3)
| Method | Path | Description |
|---|---|---|
| GET | `/api/scores/websocket/health` | WS connection counts |
| POST | `/api/scores/export/excel` | Download multi-sheet Excel workbook |
| POST | `/api/scores/broadcast` | Manual top-20 snapshot push |
| POST | `/api/scores/recalculate` | Full score recalculation |

---

## 5. Health Checks

Add to each service's `docker-compose.prod.yml` entry:

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5003/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 30s
```

---

## 6. Database Initialization

Phase 3 services use `EnsureCreatedAsync()` — no migration files needed.
Each service creates its own schema on first startup:

```bash
# ── Create databases manually (optional, speeds up first boot) ───────────────
docker compose exec mysql mysql -uroot -p$MYSQL_ROOT_PASSWORD -e \
  "CREATE DATABASE IF NOT EXISTS cma_products;" \
  "CREATE DATABASE IF NOT EXISTS cma_matching;" \
  "CREATE DATABASE IF NOT EXISTS cma_scoring;" \
  "CREATE DATABASE IF NOT EXISTS cma_notifications;" \
  "CREATE DATABASE IF NOT EXISTS cma_scraping;"
```

---

## 7. Running the k6 Load Test

```bash
# ── Against local gateway ────────────────────────────────────────────────────
GATEWAY_URL=http://localhost:8080 \
SCORING_URL=http://localhost:5003 \
  k6 run k6/load-test-scoring.js

# ── Against staging environment ──────────────────────────────────────────────
GATEWAY_URL=https://staging-api.crossmarket.example.com \
SCORING_URL=https://staging-scoring.crossmarket.example.com \
  k6 run --out json=k6-results.json k6/load-test-scoring.js
```

**k6 thresholds:**
- `p(95) < 500ms` — Response time at 95th percentile
- `p(99) < 1000ms` — Response time at 99th percentile
- `avg < 200ms` — Average response time
- `errors.rate < 0.01` — Less than 1% error rate

---

## 8. Phase 3 HS Code & Tariff Data

The `TariffService` ships with hardcoded Vietnam MFN rates for common HS codes.
To refresh from Vietnam Customs API (v2):

```bash
# Trigger a tariff table refresh (后台 job)
curl -X POST http://scoring-service:5003/api/scores/recalculate
```

> **TODO (v2):** Replace hardcoded defaults in `TariffService.VietnamDefaultRates`
> with live data from Vietnam Customs official API.

---

## 9. Rollback Procedure

```bash
# ── Identify the previous image tag ─────────────────────────────────────────
docker images crossmarket/scoring-service

# ── Roll back to previous version ─────────────────────────────────────────────
docker compose pull scoring-service:1.4.0
docker compose up -d scoring-service

# ── Verify rollback ────────────────────────────────────────────────────────────
curl http://localhost:5003/health
```

---

## 10. Monitoring & Observability

All Phase 3 services emit structured logs via Serilog (configured by `UseCommonLogging()`).

**Key log events:**
| Event | Log Level | Meaning |
|---|---|---|
| `WebSocket connection established` | INFO | New WS client connected |
| `Broadcast {Type} to {Count} clients` | DEBUG | Score broadcast sent |
| `Tariff lookup {HsCode} ({Country}): {Rate}%` | DEBUG | Tariff cache hit |
| `FedEx/DHL quote for {Weight}kg: ${Rate}` | INFO | Shipping quote returned |

**Key Prometheus metrics** (exposed at `/metrics` if enabled):
- `http_requests_total{method, path, status}` — Request counter
- `http_request_duration_seconds` — Latency histogram
- `scoring_composite_score` — Histogram of composite scores

---