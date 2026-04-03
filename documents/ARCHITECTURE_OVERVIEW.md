# ARCHITECTURE OVERVIEW
## CrossMarket Price Analyzer — System Design Document

**Version:** 1.0 | **Date:** April 3, 2026 | **Status:** Draft

---

## Table of Contents
1. [System Purpose & Vision](#1-system-purpose--vision)
2. [High-Level Architecture](#2-high-level-architecture)
3. [Frontend Subsystem](#3-frontend-subsystem)
4. [Backend Subsystem — Microservices](#4-backend-subsystem--microservices)
5. [Data Layer](#5-data-layer)
6. [Infrastructure & Cross-Cutting Concerns](#6-infrastructure--cross-cutting-concerns)
7. [Data Flow & Integration](#7-data-flow--integration)
8. [Security Architecture](#8-security-architecture)
9. [Non-Functional Requirements Mapping](#9-non-functional-requirements-mapping)

---

## 1. System Purpose & Vision

CrossMarket Price Analyzer is a web-based platform that identifies profitable U.S. → Vietnam cross-border trade opportunities. The system continuously scrapes prices from U.S. retail/wholesale sources and Vietnamese e-commerce platforms, normalizes data across currencies and packaging formats, calculates fully-loaded landed costs, and ranks products by profit margin potential.

**Design Guiding Principles:**
- **Modularity:** Each domain (Scraping, Matching, Pricing, Scoring) is an independent service
- **Observability:** Structured logging, metrics, and distributed tracing across all services
- **Scalability:** Horizontal scaling of scraping and API workers independently
- **Maintainability:** DDD on backend, clear component boundaries on frontend

---

## 2. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENTS                                  │
│                   Web Browser (React SPA)                        │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTPS / REST / WebSocket
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                       API GATEWAY                                │
│              (Ocelot or YARP — Rate Limiting, Auth)              │
└──┬──────────────┬──────────────┬──────────────┬─────────────────┘
   │              │              │              │
   ▼              ▼              ▼              ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐
│ Product  │ │ Matching │ │ Scoring  │ │ Notification │
│ Service  │ │ Service  │ │ Service  │ │   Service    │
└────┬─────┘ └────┬─────┘ └────┬─────┘ └──────┬───────┘
     │            │            │               │
     ▼            ▼            ▼               ▼
┌──────────────────────────────────────────────────────────┐
│                     MySQL Database                          │
│         (Products · Matches · Scores · Users)              │
└──────────────────────────────────────────────────────────┘
     │
     ▼
┌──────────────────────────────────────────────────────────┐
│              Message Broker (RabbitMQ / Kafka)           │
└──┬──────────────┬──────────────┬──────────────────────────┘
   │              │              │
   ▼              ▼              ▼
┌──────────┐ ┌──────────┐ ┌──────────┐
│ Scraping │ │  Cost    │ │ Exchange  │
│  Worker  │ │ Calculator│ │  Worker  │
└──────────┘ └──────────┘ └──────────┘
```

### Architecture Layers

| Layer | Description | Technology |
|---|---|---|
| **Client** | Single-page web application | React 18 (JavaScript) |
| **Gateway** | API routing, auth, rate limiting | YARP (.NET API Gateway) |
| **Application** | 5 domain microservices | .NET 9.0 |
| **Messaging** | Async event bus | RabbitMQ |
| **Data** | Primary relational store | MySQL 8.0 |
| **Cache** | Hot data, sessions, rate limits | Redis |
| **Workers** | Background job processors | .NET Worker Services |
| **Infrastructure** | Observability & deployment | Docker, Docker Compose |

---

## 3. Frontend Subsystem

*(Full detail: see `FRONTEND_DESIGN.md`)*

### Key Design Decisions
- **Framework:** React 18 (JavaScript) — component-based, rich ecosystem
- **State:** React Query (server state) + Zustand (client state)
- **Routing:** React Router v6
- **Charts:** Recharts
- **Build:** Vite
- **UI:** Tailwind CSS + shadcn/ui components

### Pages
1. **Dashboard** — Opportunity feed ranked by composite score
2. **Product Comparison** — Side-by-side U.S. vs Vietnam pricing detail
3. **Category Explorer** — Browse by HS code category
4. **Price History** — Line chart trends over time
5. **URL Quick Lookup** — Paste U.S. product URL → instant analysis card
6. **Export Center** — CSV / Excel / PDF export
7. **Settings** — Factor weight sliders, notification preferences

---

## 4. Backend Subsystem — Microservices

*(Full detail: see `BACKEND_DESIGN.md`)*

### Microservice Catalog

| # | Service | Responsibility | Key Domain |
|---|---|---|---|
| 1 | **ProductService** | Master product catalog, price snapshots | Products, Brands |
| 2 | **MatchingService** | Fuzzy/US↔VN product pairing | Matches, Confidence |
| 3 | **ScoringService** | Multi-factor ranking & opportunity scores | Scores, Rankings |
| 4 | **NotificationService** | Alerts, email, Telegram, in-app | Alerts, Subscriptions |
| 5 | **ScrapingService** | Scheduled scraping workers | *(Background only — no HTTP API)* |

### Technology Stack
- **Runtime:** .NET 9.0
- **Architecture:** DDD (Domain-Driven Design) + CQRS
- **ORM:** Entity Framework Core 9 (MySQL provider)
- **Messaging:** RabbitMQ + MassTransit
- **API:** Minimal APIs + Carter for modular endpoints
- **Serialization:** System.Text.Json (JSON)
- **Resilience:** Polly (retry, circuit breaker)
- **Validation:** FluentValidation

### DDD Bounded Contexts

```
Solution: CrossMarketAnalyzer
│
├── src/
│   ├── Domain/                  # Shared kernel (value objects, enums)
│   ├── Common/                  # Cross-cutting (behaviors, filters)
│   │
│   ├── Services/
│   │   ├── ProductService.Api/
│   │   │   ├── Domain/          # ProductAggregate, PriceSnapshot entity
│   │   │   ├── Application/     # Commands, Queries, Handlers
│   │   │   ├── Infrastructure/ # EF Core repos, Redis cache
│   │   │   └── Api/             # Controllers, DTOs
│   │   │
│   │   ├── MatchingService.Api/
│   │   │   ├── Domain/          # ProductMatch entity, MatchConfidence value object
│   │   │   ├── Application/     # Commands, Queries, Handlers
│   │   │   ├── Infrastructure/  # EF Core, Fuzzy matching impl
│   │   │   └── Api/             # Controllers, DTOs
│   │   │
│   │   ├── ScoringService.Api/
│   │   │   ├── Domain/          # OpportunityScore, ScoringFactors value objects
│   │   │   ├── Application/     # Commands, Queries, Handlers
│   │   │   ├── Infrastructure/  # EF Core, scoring engine
│   │   │   └── Api/             # Controllers, DTOs
│   │   │
│   │   ├── NotificationService.Api/
│   │   │   ├── Domain/          # Alert, Subscription entities
│   │   │   ├── Application/     # Commands, Queries, Handlers
│   │   │   ├── Infrastructure/  # EF Core, email/Telegram integrations
│   │   │   └── Api/             # Controllers, DTOs
│   │   │
│   │   └── ScrapingService.Worker/
│   │       ├── Domain/          # ScrapeJob, SourceConfig entities
│   │       ├── Application/      # Job orchestration
│   │       └── Infrastructure/   # Playwright, Scrapy, shop API clients
│   │
│   └── Infrastructure/         # Shared infra (logging, health checks)
```

---

## 5. Data Layer

### Database: MySQL 8.0

All services share a single MySQL instance with **schema-per-service** or **database-per-service** pattern:

| Service | Database | Tables |
|---|---|---|
| ProductService | `cma_products` | `products`, `brands`, `categories`, `price_snapshots`, `exchange_rates` |
| MatchingService | `cma_matching` | `product_matches`, `match_confirmations` |
| ScoringService | `cma_scoring` | `opportunity_scores`, `scoring_configs` |
| NotificationService | `cma_notifications` | `alerts`, `subscriptions`, `user_preferences` |
| ScrapingService | `cma_scraping` | `scrape_jobs`, `source_configs`, `scrape_logs` |

### Key Tables

```sql
-- products: master product records
products (id PK, name, brand, sku, category_id, hs_code, created_at, updated_at)

-- price_snapshots: time-series price data
price_snapshots (id PK, product_id FK, price, currency, source, scraped_at, unit_price)

-- product_matches: US ↔ Vietnam product pairings
product_matches (id PK, us_product_id FK, vn_product_id FK, confidence_score, status, created_at)

-- opportunity_scores: ranked opportunity records
opportunity_scores (id PK, match_id FK, margin_pct, demand_score, competition_score,
                    price_stability, composite_score, calculated_at)

-- exchange_rates: cached forex rates
exchange_rates (id PK, from_currency, to_currency, rate, fetched_at)

-- alerts: notification records
alerts (id PK, user_id, match_id FK, message, type, is_read, created_at)

-- scrape_jobs: scraping task records
scrape_jobs (id PK, source, status, started_at, completed_at, items_scraped, error_message)
```

### Redis Usage
- Exchange rate cache (TTL: 1 hour)
- API rate limiting counters
- Session tokens / JWT blacklist
- Hot product lists (top 100 opportunities, TTL: 5 min)
- Scraping job deduplication

---

## 6. Infrastructure & Cross-Cutting Concerns

### Docker Compose Services

```yaml
services:
  # Frontend
  frontend:        # React SPA (nginx serving build)

  # Gateway
  gateway:         # YARP API Gateway

  # Microservices
  product-api:     # ProductService HTTP API
  matching-api:    # MatchingService HTTP API
  scoring-api:     # ScoringService HTTP API
  notification-api:# NotificationService HTTP API

  # Workers
  scraping-worker: # Background scraper
  cost-worker:     # Landed cost calculator
  score-worker:    # Scoring engine
  exchange-worker: # Forex rate updater

  # Infrastructure
  mysql:           # MySQL 8.0
  redis:           # Redis 7
  rabbitmq:        # RabbitMQ 3.12
  nginx:           # Reverse proxy + SSL termination
```

### Observability

| Concern | Tool | What |
|---|---|---|
| **Logging** | Serilog → Elasticsearch | Structured logs from all services |
| **Metrics** | Prometheus | Custom business metrics + runtime |
| **Dashboards** | Grafana | Pre-built dashboards per service |
| **Tracing** | OpenTelemetry → Jaeger | Distributed request traces |
| **Health** | Health checks via `/health` | Liveness + Readiness probes |
| **Alerting** | Grafana Alerting | Scraping failures, high error rates |

### CI/CD (GitHub Actions)

```
Push → CI Pipeline:
  1. dotnet build / npm build
  2. Unit tests (xUnit / Vitest)
  3. Integration tests (Testcontainers)
  4. SonarQube analysis
  5. Docker image build + push to registry
  6. Deploy to staging (docker-compose)

Push to main → CD Pipeline:
  1. Run full E2E tests (Playwright)
  2. Deploy to production
```

---

## 7. Data Flow & Integration

### Happy-Path Data Flow

```
1. [ScrapingWorker]
   Scrapy/Playwright scrapes U.S. source → ProductService
   Shopee/Lazada API → ProductService

2. [ProductService]
   Normalizes product data (currency, unit price)
   Saves to MySQL → Publishes "ProductScraped" event to RabbitMQ

3. [CostWorker] (reacts to ProductScraped)
   Calculates landed cost: US Price + Shipping + Duty + VAT + Handling
   Saves cost_estimate → Publishes "CostCalculated"

4. [MatchingService] (reacts to CostCalculated)
   Fuzzy matches US product ↔ Vietnam listings
   Saves match with confidence score
   Publishes "MatchCreated"

5. [ScoringService] (reacts to MatchCreated)
   Applies weighted multi-factor scoring
   Saves opportunity_score
   Publishes "OpportunityRanked"

6. [NotificationService] (reacts to OpportunityRanked)
   Checks if score meets user alert thresholds
   Sends alerts via configured channel

7. [Frontend]
   React Query polls /api/opportunities every 60s
   WebSocket for push updates on new high-value opportunities
```

### Event Catalog

| Event | Producer | Consumers |
|---|---|---|
| `ProductScraped` | ScrapingService | ProductService, CostWorker |
| `CostCalculated` | CostWorker | MatchingService |
| `MatchCreated` | MatchingService | ScoringService |
| `OpportunityRanked` | ScoringService | NotificationService, Frontend (WS) |
| `ExchangeRateUpdated` | ExchangeWorker | ProductService, CostWorker |
| `AlertTriggered` | NotificationService | — |

---

## 8. Security Architecture

| Layer | Mechanism |
|---|---|
| **Transport** | HTTPS (TLS 1.3) enforced at nginx/load balancer |
| **API Gateway** | JWT Bearer token validation (Auth0 / IdentityServer) |
| **Service-to-Service** | mTLS or internal JWT for inter-service calls |
| **Input Validation** | FluentValidation on all API inputs |
| **Rate Limiting** | Redis-backed sliding window (100 req/min per user) |
| **Scraping** | Respect robots.txt; rotating proxies; rate limiting |
| **Secrets** | Docker secrets / Azure Key Vault / AWS SSM |
| **Dependencies** | Dependabot + Snyk scanning |

---

## 9. Non-Functional Requirements Mapping

| NFR | Target | Implementation |
|---|---|---|
| Dashboard load | < 2s | Redis cache hot data; CDN for static assets |
| API response (cached) | < 500ms | Redis caching; read replicas for MySQL |
| Scalability | 50,000+ products | Horizontal scaling of workers + API replicas |
| Availability | 99.5% uptime | Graceful degradation; health checks; circuit breakers |
| Price freshness | Daily refresh | Celery Beat / Quartz scheduled jobs |
| Exchange rate | Hourly | ExchangeWorker with 1h TTL in Redis |
| Data freshness | Every 6h (v2) | ScrapingService configurable cron per source |
| Backup | Daily, 30-day retention | MySQL binlog backup + snapshot to S3 |
