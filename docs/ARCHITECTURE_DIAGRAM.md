# Architecture Diagram — CrossMarket Price Analyzer

> **Status:** Phase 0 Draft | **Date:** April 6, 2026

---

## 1. System Landscape

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                              CLIENTS                                          ║
║                    Web Browser (React SPA)                                   ║
║                    Mobile App (future)                                       ║
╚══════════════════╤═════════════════════════════════════════════════════════╝
                   │ HTTPS / REST / WebSocket
                   ▼
╔══════════════════════════════════════════════════════════════════════════════╗
║                        API GATEWAY (YARP)                                    ║
║         Rate Limiting · JWT Auth · Request Routing · Health Checks            ║
╚═══╤══════════╤══════════╤══════════╤═══════════════════════════════════════╝
    │          │          │          │
    │          │          │          └──────────────────┐
    ▼          ▼          ▼                             ▼
┌─────────┐ ┌─────────┐ ┌─────────┐            ┌────────────────┐
│Product  │ │Matching │ │Scoring  │            │Notification    │
│Service  │ │Service  │ │Service  │            │Service         │
│ :5001   │ │ :5002   │ │ :5003   │            │ :5004          │
└────┬────┘ └────┬────┘ └────┬────┘            └───────┬────────┘
     │           │           │                         │
     │           │           │                         │
     └───────────┴─────┬─────┴─────────────────────────┘
                       │ RabbitMQ Events
                       ▼
        ┌──────────────────────────────────────────┐
        │              MESSAGE BUS                 │
        │         RabbitMQ 3.12 · Exchange         │
        └──────────────────┬───────────────────────┘
                           │ Events
           ┌───────────────┼───────────────┬───────────────┐
           │               │               │               │
           ▼               ▼               ▼               ▼
    ┌─────────────┐ ┌──────────┐ ┌──────────────────┐ ┌──────────┐
    │  Scraping   │ │  Cost    │ │    Scoring       │ │  Alert   │
    │  Worker     │ │  Worker  │ │    Worker        │ │  Worker  │
    └─────────────┘ └──────────┘ └──────────────────┘ └──────────┘

        ┌──────────────────────────────────────────────────────────┐
        │                    DATA LAYER                            │
        │                                                           │
        │   ┌──────────┐  ┌──────────┐  ┌───────────────────────┐   │
        │   │  MySQL   │  │  Redis   │  │  Elasticsearch (Logs) │   │
        │   │   8.0    │  │    7     │  │                       │   │
        │   └──────────┘  └──────────┘  └───────────────────────┘   │
        └──────────────────────────────────────────────────────────┘
```

---

## 2. Docker Compose Topology

```
┌─────────────────────────────────────────────────────────────────┐
│                     DOCKER COMPOSE CLUSTER                       │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │  MySQL   │  │  Redis   │  │ RabbitMQ │  │ Nginx    │       │
│  │  8.0     │  │  7-alpine│  │3.12+mgmt│  │ (reverse)│       │
│  │  :3306   │  │  :6379   │  │:5672/72 │  │  :80     │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                    API SERVICES (replicated)             │    │
│  │  gateway  product-api  matching-api  scoring-api  ...  │    │
│  │    :8080      :5001        :5002         :5003          │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                    WORKER SERVICES                      │    │
│  │  scraping-worker  cost-worker  score-worker  exchange-  │    │
│  │                                  worker                 │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────────────────┐   │
│  │Frontend  │  │Prometheus│  │Grafana                       │   │
│  │(nginx)   │  │:9090     │  │:3000                         │   │
│  │  :3000   │  └──────────┘  └──────────────────────────────┘   │
│  └──────────┘                                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Request/Response Flow

```
  Browser
    │
    │  ① HTTPS GET /api/opportunities
    ▼
  YARP Gateway  ─── rate limit check ─── JWT validation
    │
    │  ② Forward to ProductService:5001
    ▼
  ProductService.API
    │  ③ React Query cache hit? ──Yes── return cached data
    │         │
    │        No
    │         │
    │  ④ Query MySQL (cached result)
    │  ⑤ Query ScoringService (HTTP internal)
    │  ⑥ Aggregate results, serialize to DTOs
    ▼
  Response ── JSON ── React SPA renders
```

---

## 4. Event-Driven Flow

```
┌───────────────┐      ProductScraped       ┌────────────────┐
│ Scraping      │ ─────────────────────────▶ │ ProductService │
│ Worker        │                           │ (normalizes)   │
└───────────────┘                           └───────┬────────┘
                                                    │
                                          Published to RabbitMQ
                                                    │
                                    ┌───────────────┼───────────────┐
                                    │               │               │
                                    ▼               ▼               ▼
                            ┌──────────┐   ┌───────────┐   ┌────────────┐
                            │ Matching │   │ Cost      │   │ Scoring    │
                            │ Service  │   │ Worker    │   │ Worker     │
                            └────┬─────┘   └─────┬─────┘   └─────┬──────┘
                                 │               │               │
                                 │ MatchCreated   │ CostCalculated│
                                 └───────────────┴───────┬───────┘
                                                         │
                                                         ▼
                                              ┌──────────────────┐
                                              │ OpportunityScored│
                                              │ → WebSocket push │
                                              │ → Alert check    │
                                              └──────────────────┘
```

---

## 5. Solution Structure

```
CrossMarketAnalyzer/
│
├── src/
│   ├── Common/
│   │   ├── Common.Domain/          ← Shared kernel (no external deps)
│   │   ├── Common.Application/     ← Pipeline behaviors, interfaces
│   │   └── Common.Infrastructure/  ← EF Core, Redis, RabbitMQ, Serilog
│   │
│   ├── Services/
│   │   ├── ProductService/        ← Product catalog + price snapshots
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/               ← Minimal API + Carter
│   │   │
│   │   ├── MatchingService/       ← Fuzzy matching engine
│   │   ├── ScoringService/        ← Multi-factor scoring
│   │   ├── NotificationService/   ← Alerts + delivery
│   │   └── ScrapingService/       ← Background workers (no HTTP)
│   │
│   └── Apps/
│       ├── CMA.Gateway/            ← YARP API Gateway
│       └── CMA.WebApp/             ← React SPA
│
├── tests/
│   ├── Common.UnitTests/
│   ├── ProductService.UnitTests/
│   ├── MatchingService.UnitTests/
│   ├── ScoringService.UnitTests/
│   └── ProductService.IntegrationTests/
│
├── docker-compose.yml
├── docker-compose.override.yml
└── CrossMarketAnalyzer.sln
```

---

## 6. Component Interaction Matrix

| Component | Calls | Listens To | Persists In |
|---|---|---|---|
| **React SPA** | Gateway REST API | WebSocket | — |
| **YARP Gateway** | All service APIs | — | — |
| **ProductService** | MatchingService, ScoringService | ProductScraped | MySQL |
| **MatchingService** | ProductService (HTTP) | ProductScraped | MySQL |
| **ScoringService** | ProductService (HTTP) | MatchCreated, CostCalculated | MySQL |
| **NotificationService** | — | OpportunityScored | MySQL |
| **ScrapingService** | ProductService (HTTP) | — | MySQL |
| **Redis** | All services (cache/rate-limit) | — | Memory |
| **RabbitMQ** | — | All services | Message queue |
