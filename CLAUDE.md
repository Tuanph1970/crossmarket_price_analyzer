# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CrossMarket Price Analyzer — a .NET 9 microservices platform that identifies U.S. → Vietnam cross-border trade opportunities by scraping prices from U.S. retail sources and Vietnamese e-commerce platforms, matching products, and scoring opportunities by profit margin.

**Stack:** .NET 9, CQRS + MediatR, EF Core, MySQL, Redis, RabbitMQ/MassTransit, React SPA (Vite), YARP API Gateway

---

## Common Commands

```bash
# Build (Debug — default)
dotnet build CrossMarketAnalyzer.sln

# Build Release
dotnet build CrossMarketAnalyzer.sln --configuration Release

# Run all tests
dotnet test CrossMarketAnalyzer.sln

# Run tests for a specific project
dotnet test tests/ProductService.UnitTests/ProductService.UnitTests.csproj

# Run a specific API service
dotnet run --project src/Services/ProductService/ProductService.Api

# Start infrastructure containers (MySQL, Redis, RabbitMQ)
docker compose up -d mysql redis rabbitmq
```

> **Note:** Each service auto-creates its own database on startup via `EnsureCreatedAsync()` (Development). EF migrations are not currently used — the `dotnet ef migrations` command listed in some docs will not work as written.

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
| `CMA.Gateway` | 8080 | — | YARP API Gateway — single entry point |
| `CMA.WebApp` | 3000 | — | React SPA (static files via nginx in Docker) |

### Layer Pattern (same across all services)

```
Service/
├── Domain/           # Entities, value objects, enums, domain exceptions
├── Application/     # Commands/Queries (MediatR), Handlers, DTOs, Services
├── Infrastructure/  # EF Core DbContext, repositories, external clients
├── Contracts/       # (ProductService only) DbContext interface boundary
└── Api/             # Program.cs, Minimal API endpoints
```

### Common Shared Libraries

- **`Common.Domain`**: Shared kernel — `BaseEntity<TId>`, `AuditableEntity`, `Money`, `Percentage`, `CountryCode`, enums (`ProductSource`, `MatchStatus`, `AlertType`), `IRepository<T>`, `IUnitOfWork`. **No infrastructure dependencies.**

- **`Common.Application`**: Pipeline behaviors (`ValidationBehavior`, `LoggingBehavior`, `CachingBehavior`, `PerfBehavior`), shared interfaces (`ICacheService`, `IEventPublisher`, `IExchangeRateService`), `ServiceCollectionExtensions`.

- **`Common.Infrastructure`**: `BaseDbContext`, `RedisCacheService`, `RabbitMqEventPublisher`, Serilog config, `OpenTelemetry` setup, resilience policies. Key entry points:
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

### API Endpoints

All services use **Minimal APIs** (`.MapGet`/`.MapPost`/`.MapPut`/`.MapDelete`) with `.WithTags()`, `.WithName()`, `.WithDescription()` for Swagger. Controllers are not used.

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

---

## Key Patterns

- **Aggregate Root**: `Product`, `ProductMatch`, `OpportunityScore`, `Alert` — owns its consistency boundary
- **Repository**: `IProductRepository` (defined in Domain, implemented in Infrastructure)
- **Specification**: Reusable query filters (e.g., `ConfirmedMatchSpecification`)
- **Factory**: `OpportunityScoreFactory.Create()` for complex object construction
- **Value Objects**: `Money` (amount + currency), `Percentage` — immutable, equality by value
- **Polly**: Retry and circuit breaker on `HttpClient`s, RabbitMQ, and database calls
- **IProductDbContext**: `ProductService` uses a `Contracts/Persistence/IProductDbContext.cs` interface boundary over `ProductDbContext` — inject this for persistence abstractions

---

## Data Flow

```
ScrapingService.Worker (Quartz.NET scheduled jobs)
  → scrapers (Playwright-based: Amazon, Walmart, CigarPage)
  → ProductService.Api POST /api/products/upsert-from-scrape
  → saves product + price snapshot
  → publishes ProductScrapedEvent (RabbitMQ)

  → ScrapingService.Worker (LandedCostRecalcJob)
  → ScoringService (saves opportunity score)
  → publishes OpportunityScoredEvent
  → NotificationService (checks thresholds, sends alerts)

CMA.Gateway (YARP) → React SPA
```

---

## Important Conventions

- **`dotnet format`** is not used — style is enforced by `.editorconfig` only
- **No EF migrations** — each service uses `db.Database.EnsureCreatedAsync()` in development
- Each `Program.cs` wires its own DI: domain interfaces → infrastructure implementations
- `ScrapingService.Worker` is purely `BackgroundService` / Quartz.NET — no HTTP API
- Frontend is in `src/Apps/CMA.WebApp` (React + Vite) but served as static files in Docker
- Docker healthchecks use `infrastructure/docker/healthcheck.sh` — a simple HTTP health probe
