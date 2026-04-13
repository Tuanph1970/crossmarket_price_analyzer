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

# Run all tests
dotnet test CrossMarketAnalyzer.sln

# Run tests for a specific project
dotnet test tests/ProductService.UnitTests/ProductService.UnitTests.csproj

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
```

> Each service auto-creates its own database on startup via `EnsureCreatedAsync()`. **EF migrations are not used.**

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
| `AuthService` | — | — | Auth/identity (Domain + Application + Infrastructure + Api) |
| `CMA.Gateway` | 8080 | — | YARP API Gateway — single entry point for all services |
| `CMA.WebApp` | 3000 | — | React SPA (static files via nginx in Docker) |

### Layer Pattern (per service)

```
Service/
├── Domain/           # Entities, value objects, enums, domain exceptions
├── Application/     # Commands/Queries (MediatR), Handlers, DTOs, Services
├── Infrastructure/  # EF Core DbContext, repositories, external clients
├── Contracts/       # (ProductService only) — IProductDbContext interface boundary
└── Api/             # Program.cs, Minimal API endpoints
```

The `Contracts/` layer in ProductService exposes `IProductDbContext` (not the concrete `ProductDbContext`) so consumers of the service don't depend on EF Core internals.

### Common Shared Libraries

- **`Common.Domain`**: Shared kernel — `BaseEntity<TId>`, `AuditableEntity`, `Money`, `Percentage`, `CountryCode`, enums (`ProductSource`, `MatchStatus`, `AlertType`, `ConfidenceLevel`, `DeliveryChannel`), `IRepository<T>`, `IUnitOfWork`. **No infrastructure dependencies.**

- **`Common.Application`**: Pipeline behaviors (`ValidationBehavior`, `LoggingBehavior`, `CachingBehavior`, `PerfBehavior`), shared interfaces (`ICacheService`, `IEventPublisher`, `IExchangeRateService`, `IScraperFactory`, `IRotatingProxyService`, `IOpportunityWebSocketHandler`), `ServiceCollectionExtensions`.

- **`Common.Infrastructure`**: `BaseDbContext`, `RedisCacheService`, `RabbitMqEventPublisher`, `OutboxProcessor`, Serilog config, OpenTelemetry setup, Polly resilience policies. Key entry points:
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

`ProductService` uses `IProductScraper` implementations in `Infrastructure/Services/ProductScrapers/`:
- `AmazonScraper`
- `WalmartScraper`
- `CigarPageScraper`

`ScraperFactory` selects the correct scraper by URL pattern. Scrape results flow into the `UpsertFromScrapeAsync` service method.

### QuickLookup Flow

`POST /api/products/quick-lookup` chains multiple steps via MediatR:
1. `QuickLookupCommand` dispatches to `QuickLookupCommandHandler`
2. Handler calls `ScraperFactory` → scrapes the URL
3. Handler calls `MatchingService` (HTTP client) to find VN matches
4. Returns `QuickLookupResultDto`

### Database Strategy

Each service has its own **MySQL database** (not schema). Databases are created on container startup via `MYSQL_DATABASE` env vars. `EnsureCreatedAsync()` creates tables on first run in Development.

### Event-Driven Integration

Services communicate via RabbitMQ + MassTransit. Events are defined in each service's Application layer and consumed downstream. Each service registers its own consumers in `Program.cs`.

**Outbox pattern**: `OutboxProcessor` ensures reliable event publishing — events are written to an outbox table first, then published to RabbitMQ, preventing lost events under transient failures.

**WebSocket**: `IOpportunityWebSocketHandler` pushes real-time opportunity updates to the React SPA.

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

CMA.Gateway (YARP, :8080) → individual services (:5001–5004)
React SPA (:3000) → Gateway
```

### Event Catalog

| Event | Producer | Consumers |
|---|---|---|
| `ProductScraped` | ProductService | MatchingService, ScrapingService |
| `OpportunityScored` | ScoringService | NotificationService, Frontend (WS) |

---

## Key Patterns

- **Aggregate Root**: `Product`, `ProductMatch`, `OpportunityScore`, `Alert` — owns its consistency boundary
- **Repository**: `IProductRepository` (defined in Domain, implemented in Infrastructure)
- **Specification**: Reusable query filters (e.g., `ConfirmedMatchSpecification`)
- **Factory**: `OpportunityScoreFactory.Create()` for complex object construction
- **Value Objects**: `Money` (amount + currency), `Percentage` — immutable, equality by value
- **Polly**: Retry and circuit breaker on `HttpClient`s, RabbitMQ, and database calls
- **IProductDbContext**: ProductService uses `Contracts/Persistence/IProductDbContext.cs` as a persistence abstraction boundary

---

## Important Conventions

- **`dotnet format`** is not used — style is enforced by `.editorconfig` only
- Each `Program.cs` wires its own DI: domain interfaces → infrastructure implementations
- `ScrapingService.Worker` is purely `BackgroundService` / Quartz.NET — no HTTP API
- Frontend is in `src/Apps/CMA.WebApp` (React + Vite) but served as static files in Docker
- Docker healthchecks use `infrastructure/docker/healthcheck.sh` — a simple HTTP health probe
- All services wait for MySQL, Redis, and RabbitMQ to be `service_healthy` before starting (configured in `docker-compose.yml`)
- AuthService is planned/partial — it has Domain, Application, Infrastructure, and Api layers but may not be fully wired yet

---

## Test Projects

There are 9 test projects in `tests/`:
- `Common.UnitTests/`, `Common.IntegrationTests/`
- `ProductService.UnitTests/`, `ProductService.IntegrationTests/`
- `MatchingService.UnitTests/`, `MatchingService.IntegrationTests/`
- `ScoringService.UnitTests/`, `ScoringService.IntegrationTests/`
- `AuthService.Tests/`
