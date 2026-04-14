# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CrossMarket Price Analyzer — a .NET 9 microservices platform that identifies U.S. → Vietnam cross-border trade opportunities by scraping prices from U.S. retail sources and Vietnamese e-commerce platforms, matching products, and scoring opportunities by profit margin.

**Stack:** .NET 9, CQRS + MediatR, EF Core, MySQL, Redis, RabbitMQ/MassTransit, YARP API Gateway, React SPA (Vite)

---

## Commands

```bash
# Build (Debug — default)
dotnet build CrossMarketAnalyzer.sln

# Build Release
dotnet build CrossMarketAnalyzer.sln --configuration Release

# Run all tests (unit + integration)
dotnet test CrossMarketAnalyzer.sln

# Run unit tests only
dotnet test CrossMarketAnalyzer.sln --filter "FullyQualifiedName~UnitTests" --configuration Release

# Run tests for a specific project
dotnet test tests/ProductService.UnitTests/

# Run a specific API service (port 5001)
dotnet run --project src/Services/ProductService/ProductService.Api

# Start infrastructure only (MySQL, Redis, RabbitMQ)
docker compose up -d mysql redis rabbitmq

# Start full stack (all containers)
docker compose up -d

# Start full stack with observability (Prometheus + Grafana)
docker compose --profile observability up -d

# Frontend dev
cd src/Apps/CMA.WebApp && npm install && npm run dev

# Regenerate Swagger XML docs after API changes (from repo root)
./scripts/swagger-regen.sh              # all services
./scripts/swagger-regen.sh scoring      # specific service
```

> Each service auto-creates its own database on startup via `EnsureCreatedAsync()`. **EF migrations are not used.**
> `global.json` pins the .NET 9 SDK.

---

## Projects Layout

```
src/
├── Common/
│   ├── Common.Domain/          # Shared kernel (no infra deps)
│   ├── Common.Application/     # MediatR behaviors, shared interfaces
│   └── Common.Infrastructure/  # EF Core, Redis, RabbitMQ, Serilog, OpenTelemetry
├── Services/
│   ├── ProductService/         # Domain + Application + Infrastructure + Contracts + Api
│   ├── MatchingService/        # Domain + Application + Infrastructure + Api
│   ├── ScoringService/         # Domain + Application + Infrastructure + Api
│   ├── NotificationService/     # Domain + Application + Infrastructure + Api
│   ├── ScrapingService/         # Domain + Application + Infrastructure + Worker
│   └── AuthService/            # CrossMarket.SharedKernel + Domain + App + Infra + Api
├── Apps/
│   ├── CMA.Gateway/            # YARP reverse proxy
│   └── CMA.WebApp/             # React SPA (Vite)
tests/                          # 9 test projects mirroring service structure
documents/                       # PRD, architecture, design docs
infrastructure/docker/           # healthcheck.sh, Prometheus/Grafana configs
```

---

## Architecture

### Services & Ports

| Service | Port | Database | Purpose |
|---|---|---|---|
| `ProductService.Api` | 5001 | `cma_products` | Product catalog, price snapshots, exchange rates |
| `MatchingService.Api` | 5002 | `cma_matching` | Fuzzy US↔VN product pairing |
| `ScoringService.Api` | 5003 | `cma_scoring` | Landed cost calculation, multi-factor scoring |
| `NotificationService.Api` | 5004 | `cma_notifications` | Alerts, subscriptions, multi-channel delivery |
| `ScrapingService.Worker` | — | `cma_scraping` | Background scheduled jobs (no HTTP API) |
| `AuthService.Api` | — | — | Auth/identity — fully wired but **not deployed in docker-compose** |
| `CMA.Gateway` | 8080 | — | YARP API Gateway — single entry point |
| `CMA.WebApp` | 3000 | — | React SPA (static files via nginx in Docker) |

Inside containers all services listen on `8080`; `docker-compose.override.yml` maps published ports (5001–5004) to container port 8080 for local dev.

### YARP Gateway Routes

| Route | Target Cluster | Notes |
|---|---|---|
| `/api/products`, `/api/categories`, `/api/opportunities`, `/api/exchange-rates` | product-cluster | → ProductService :5001 |
| `/api/matches` | matching-cluster | → MatchingService :5002 |
| `/api/scores`, `/ws/opportunities` | scoring-cluster | → ScoringService :5003 |
| `/api/alerts`, `/api/subscriptions` | notification-cluster | → NotificationService :5004 |
| `/api/auth` | — | AuthService not routed in gateway (not in compose) |

### Layer Pattern (per service)

Most services follow `Domain / Application / Infrastructure / Api` (or `Worker` for ScrapingService). Two notable deviations:

**ProductService has a `Contracts/` layer** exposing `IProductDbContext` (not the concrete `ProductDbContext`) so consumers don't depend on EF Core internals. EF configurations live in `ProductService.Application/Persistence/Configurations/`.

**ScrapingService has `Infrastructure/Srapers/`** containing VN scraper implementations: HTTP API clients (`ShopeeApiClient`, `LazadaApiClient`) and Playwright-based (`TikiScraper`). US scrapers (`AmazonScraper`, `WalmartScraper`, `CigarPageScraper`) are registered in the Worker's `Program.cs`.

**Gateway** (`CMA.Gateway`) has no business logic — YARP routes are entirely defined in `appsettings.json` under the `ReverseProxy` section.

AuthService has its own `CrossMarket.SharedKernel/` sub-project (separate from `Common/`), containing `JwtSettings` and `PasswordHasher`.

### Common Shared Libraries

- **`Common.Domain`**: Shared kernel — `BaseEntity<TId>`, `AuditableEntity`, `Money`, `Percentage`, `CountryCode`, enums (`ProductSource`, `MatchStatus`, `AlertType`, `ConfidenceLevel`, `DeliveryChannel`), `IRepository<T>`, `IUnitOfWork>`. **No infrastructure dependencies.**

- **`Common.Application`**: Pipeline behaviors (`ValidationBehavior`, `LoggingBehavior`, `CachingBehavior`, `PerfBehavior`), shared interfaces (`ICacheService`, `IEventPublisher`, `IExchangeRateService`, `IScraperFactory`, `IRotatingProxyService`, `IOpportunityWebSocketHandler`), `ServiceCollectionExtensions`.

- **`Common.Infrastructure`**: `BaseDbContext`, `RedisCacheService`, `RabbitMqEventPublisher`, `OutboxProcessor`, Serilog config, OpenTelemetry setup, Polly resilience policies. Entry points:
  - `UseCommonLogging()` — called on `builder.Host` to configure Serilog + OpenTelemetry
  - `AddCommonInfrastructure(config, serviceName)` — called on `builder.Services` to register DB, Redis, RabbitMQ, health checks
  - `AddCommonApplication()` — called on `builder.Services` to register MediatR + all pipeline behaviors
  - `AddStandardResilienceHandler()` — Polly retry/circuit-breaker/timeout for `HttpClient`

### CQRS + MediatR

All business logic goes through MediatR:
- **Commands** (`IRequest<TResponse>`) in `Commands/` — writes
- **Queries** (`IRequest<TResponse>`) in `Queries/` — reads
- Handlers live in `Handlers/` alongside their command/query
- Pipeline order: `LoggingBehavior → ValidationBehavior → CachingBehavior → PerfBehavior → Handler`

Validation is done via **FluentValidation**. All API uses **Minimal APIs** (`.MapGet`/`.MapPost`/`.MapPut`/`.MapDelete`) with Swagger attributes. Controllers are not used.

### Scraper Pattern

`ProductService` uses `IProductScraper` implementations in `Infrastructure/Services/ProductScrapers/`: `AmazonScraper`, `WalmartScraper`, `CigarPageScraper`. `ScraperFactory` selects by URL pattern.

`ScrapingService.Worker` is a **background worker** using `Host.CreateApplicationBuilder` (not `WebApplication.CreateBuilder`). VN scrapers: `ShopeeApiClient`, `LazadaApiClient` (HTTP), `TikiScraper` (Playwright). US scrapers reuse the same `IProductScraper` contracts. Named `HttpClient`s: `"ProductService"`, `"ScoringService"`, `"ExchangeRate"`, `"Shopee"`, `"Lazada"`, `"RotatingProxyHealthCheck"` — all with Polly resilience. Quartz.NET drives scheduled jobs via `AddQuartzJobs()`.

### QuickLookup Flow

`POST /api/products/quick-lookup` chains multiple steps via MediatR:
1. `QuickLookupCommand` dispatches to `QuickLookupCommandHandler`
2. Handler calls `ScraperFactory` → scrapes the URL
3. Handler calls `MatchingService` (HTTP client) to find VN matches
4. Returns `QuickLookupResultDto`

### ScoringService WebSocket

`ScoringService` exposes a WebSocket endpoint at `/ws/opportunities` (via `OpportunityWebSocketHandler` singleton + hosted service). Health at `/api/scores/websocket/health`. Frontend connects via raw WebSocket (not SignalR); `useWebSocket.js` and `useRealtimeOpportunities.js` merge WS updates into the React Query cache.

### NotificationService Internals

NotificationService does **not** use MediatR — endpoints inject services directly. Two background hosted services:
- **`AlertThresholdEngine`**: monitors opportunity thresholds continuously
- **`ScheduledReportWorker`**: generates PDF/CSV reports on schedule using QuestPDF

Delivery channels: **Email** (SendGrid HTTP), **Telegram** (Bot API). Bot token: `builder.Configuration["Telegram:BotToken"]`.

### Database Strategy

Each service has its own **MySQL database** (not schema). Databases are created on container startup via `MYSQL_DATABASE` env vars. `EnsureCreatedAsync()` creates tables on first run in Development. All services use `ServerVersion.AutoDetect(cs)` for MySQL version detection.

### Event-Driven Integration

Services communicate via RabbitMQ + MassTransit. Events are defined in each service's Application layer and consumed downstream. Each service registers its own consumers in `Program.cs`.

**Outbox pattern**: `OutboxProcessor` ensures reliable event publishing — events are written to an outbox table first, then published to RabbitMQ, preventing lost events under transient failures.

### API Endpoints (key not-self-evident routes)

| Endpoint | Service | Notes |
|---|---|---|
| `POST /api/products/quick-lookup` | ProductService | Scraper → MatchingService HTTP |
| `POST /api/products/upsert-from-scrape` | ProductService | Called by ScrapingService |
| `PUT /api/scores/manual-costs` | ScoringService | Store landed-cost overrides |
| `POST /api/scores/export/excel` | ScoringService | Returns `.xlsx` via ClosedXML |
| `POST /api/scores/broadcast` | ScoringService | Broadcast score update via WS |
| `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh` | AuthService | JWT access + refresh tokens |
| `GET/POST/DELETE /api/watchlist` | AuthService | Auth-gated watchlist CRUD |
| `GET/POST/PUT/DELETE /api/alerts/thresholds` | AuthService | Auth-gated threshold CRUD |

---

## Data Flow

```
ScrapingService.Worker (Quartz.NET scheduled jobs)
  → Playwright scrapers (Amazon, Walmart, CigarPage)
  → ProductService.Api POST /api/products/upsert-from-scrape
  → saves product + price snapshot
  → publishes ProductScrapedEvent via outbox (RabbitMQ)
    → MatchingService (fuzzy US↔VN match)
    → ScoringService (landed cost + composite score)
    → publishes OpportunityScoredEvent (RabbitMQ)
    → NotificationService (checks thresholds, sends alerts)
    → ScoringService WebSocket (/ws/opportunities) → React SPA

CMA.Gateway (YARP, :8080) → individual services (:5001–5004)
React SPA (:3000) → Gateway
```

### Event Catalog

| Event | Producer | Consumers |
|---|---|---|
| `ProductScraped` | ProductService | MatchingService, ScrapingService |
| `OpportunityScored` | ScoringService | NotificationService, Frontend (WS) |

---

## Frontend Architecture (`src/Apps/CMA.WebApp`)

**Stack:** React 18 + Vite 5, Tailwind CSS 3.4, React Router 6, React Query 5, Zustand 5 (with `persist` for JWT in `localStorage`), React Hook Form + Zod, Recharts 2, i18next, Axios, Sonner (toasts), Playwright (e2e), Vitest.

- **Auth tokens**: stored in `cma-auth-store` via Zustand persist; Axios interceptor attaches `Authorization: Bearer` header and handles silent refresh on 401.
- **WebSocket**: `useWebSocket.js` manages raw WebSocket; `useRealtimeOpportunities.js` merges updates into React Query cache.
- **i18n**: `i18next` with `en.json` / `vi.json` language files.
- **API base URL**: `VITE_API_BASE_URL` env var; dev proxy in `vite.config.js` routes `/api/products`, `/api/scores`, `/api/matches` to individual services (not the gateway). Production uses `VITE_API_BASE_URL` pointing at the gateway or direct services.
- **State**: `authStore.js` (auth/JWT), `filterStore.js` (UI filters), `scoringStore.js`, `uiStore.js`.

---

## Key Patterns

- **Aggregate Root**: `Product`, `ProductMatch`, `OpportunityScore`, `Alert` — owns its consistency boundary
- **Repository**: `IProductRepository` (defined in Domain, implemented in Infrastructure)
- **Specification**: Reusable query filters (e.g., `ConfirmedMatchSpecification`)
- **Factory**: `OpportunityScoreFactory.Create()` for complex object construction
- **Value Objects**: `Money` (amount + currency), `Percentage` — immutable, equality by value
- **Polly**: Retry and circuit breaker on `HttpClient`s, RabbitMQ, and database calls
- **IProductDbContext**: ProductService uses `Contracts/Persistence/IProductDbContext.cs` as a persistence abstraction boundary
- **JWT auth (AuthService)**: `JwtTokenGenerator` uses 1-minute clock skew; `JwtBearerEvents` supports token-from-query-string for `/ws/notifications` SignalR path (reserved for future use)

---

## CI/CD

**`.github/workflows/ci.yml`**: Runs on push to `main, develop, feature/**, fix/**` and PRs to `main, develop`.
- Backend: .NET 9, MySQL/Redis/RabbitMQ service containers, Restore → Build (Release) → Unit tests (filter `~UnitTests`) → TRX results → SonarQube (PR only)
- Frontend: Node 20, `npm ci`, ESLint (`|| true`), Build, Vitest unit tests
- Docker Buildx builds all 6 images and pushes to GHCR on `main/develop`

**`.github/workflows/cd.yml`**: On push to `main` or manual `workflow_dispatch`.
- Builds + pushes 7 images (tagged with Git SHA prefix)
- **Deploy Staging**: SSH to `STAGING_HOST`, writes `docker-compose.prod.yml` override, pulls + restarts
- **Smoke Test Staging**: `curl` health checks on gateway + all 4 API services
- **Deploy Production**: Manual approval gate → SSH deploy → health check loop (15 retries)

**`k6/load-test-scoring.js`**: k6 load test script with custom metrics, stages, and summary output.

---

## Infrastructure Docker Assets

`infrastructure/docker/`:
- **`healthcheck.sh`**: `curl -f http://localhost:8080/health` — mounted as read-only into all container healthchecks
- **`prometheus/prometheus.yml`**: Scrapes `product-api`, `matching-api`, `scoring-api`, `notification-api`, `gateway` at `/metrics`; also scrapes RabbitMQ at `rabbitmq:15692` (HTTP management plugin)
- **`grafana/provisioning/dashboards/cma.json`**: Pre-provisioned Grafana dashboard
- **`grafana/provisioning/datasources/datasources.yml`**: Auto-provisions Prometheus datasource

---

## Important Conventions

<<<<<<< HEAD
- **No `.editorconfig`**: there is no `.editorconfig` file — code style follows default .NET conventions only
- Each `Program.cs` wires its own DI: domain interfaces → infrastructure implementations
- `ScrapingService.Worker` uses `Host.CreateApplicationBuilder` (Worker pattern); all other services use `WebApplication.CreateBuilder`
- Frontend is in `src/Apps/CMA.WebApp` (React + Vite) but served as static files in Docker
- Docker healthchecks use `infrastructure/docker/healthcheck.sh`
- All services wait for MySQL, Redis, and RabbitMQ to be `service_healthy` before starting (configured in `docker-compose.yml`)
- AuthService.Api is fully implemented but **not listed in docker-compose** — it exists but is not deployed in the current stack
=======
- Style is enforced by `.editorconfig` only — **`dotnet format` is not used**
- Each `Program.cs` wires its own DI: domain interfaces → infrastructure implementations
- `ScrapingService.Worker` uses `BackgroundService` + Quartz.NET — no HTTP API
- Gateway routing is entirely config-driven (`appsettings.json` `ReverseProxy` section), not code
- Docker healthchecks use `infrastructure/docker/healthcheck.sh` — a simple HTTP probe curling `/health` on port 8080
- All services wait for MySQL, Redis, and RabbitMQ to be `service_healthy` before starting (configured in `docker-compose.yml`)

---

## Test Projects

```
tests/
├── Common.UnitTests/           # Common.Domain shared types
├── Common.IntegrationTests/   # Common.Infrastructure
├── ProductService.UnitTests/
├── ProductService.IntegrationTests/
├── MatchingService.UnitTests/
├── MatchingService.IntegrationTests/
├── ScoringService.UnitTests/
├── ScoringService.IntegrationTests/
├── AuthService.Tests/
└── benchmark/                 # BenchmarkDotNet benchmarks
```

Integration tests require the full Docker stack (MySQL, Redis, RabbitMQ). The CI filter `FullyQualifiedName~UnitTests` excludes integration tests — adjust as needed.
