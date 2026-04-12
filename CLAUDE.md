# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CrossMarket Price Analyzer — a .NET 9 microservices platform that identifies U.S. → Vietnam cross-border trade opportunities by scraping prices from U.S. retail sources and Vietnamese e-commerce platforms, matching products, and scoring opportunities by profit margin.

**Stack:** .NET 9, CQRS + MediatR, EF Core, MySQL, Redis, RabbitMQ/MassTransit, React SPA, YARP API Gateway

---

## Common Commands

```bash
# Restore + build
dotnet build CrossMarketAnalyzer.sln

# Test all
dotnet test CrossMarketAnalyzer.sln

# Test a specific project
dotnet test tests/ProductService.UnitTests/ProductService.UnitTests.csproj

# Run a specific service
dotnet run --project src/Services/ProductService/ProductService.Api

# Run infrastructure containers
docker compose up -d mysql redis rabbitmq

# EF migrations (run from solution root)
dotnet ef migrations add <Name> \
  --project src/Common/Common.Infrastructure \
  --startup-project src/Services/ProductService/ProductService.Api
```

---

## Architecture

### Microservices (each follows DDD layers)

| Service | Port | Purpose |
|---|---|---|
| `ProductService.Api` | 5001 | Product catalog, price snapshots, exchange rates |
| `MatchingService.Api` | 5002 | Fuzzy US↔VN product pairing |
| `ScoringService.Api` | 5003 | Landed cost calculation, multi-factor scoring |
| `NotificationService.Api` | 5004 | Alerts, subscriptions, multi-channel delivery |
| `ScrapingService.Worker` | — | Background scheduled jobs (no HTTP API) |
| `CMA.Gateway` | 8080 | YARP API Gateway (single entry point) |

### Layer Pattern (same across all services)

Each service follows `Domain → Application → Infrastructure → Api`:
- **Domain**: Aggregate roots, entities, value objects, enums, domain exceptions
- **Application**: Commands/Queries (MediatR `IRequest`), Handlers, DTOs, Services, Pipeline Behaviors
- **Infrastructure**: EF Core DbContext, repositories, external clients (Redis, RabbitMQ)
- **Api/Worker**: `Program.cs`, Minimal API endpoints or Quartz.NET jobs

### Common Shared Libraries

- **`Common.Domain`**: Shared kernel — `BaseEntity<TId>`, `AuditableEntity`, value objects (`Money`, `Percentage`, `CountryCode`), enums (`ProductSource`, `MatchStatus`, `AlertType`, etc.), `IRepository<T>`, `IUnitOfWork`
- **`Common.Application`**: Pipeline behaviors (`ValidationBehavior`, `LoggingBehavior`, `CachingBehavior`, `PerfBehavior`), shared interfaces (`ICacheService`, `IEventPublisher`, `IExchangeRateService`), `ServiceCollectionExtensions`
- **`Common.Infrastructure`**: EF Core `BaseDbContext`, `RedisCacheService`, `RabbitMqEventPublisher`, Serilog config, telemetry

### CQRS + MediatR Pattern

All business logic goes through MediatR:
- **Commands** (`IRequest<TResponse>`) in `Commands/` folder — writes/modifications
- **Queries** (`IRequest<TResponse>`) in `Queries/` folder — reads
- Handlers live in `Handlers/` subfolder alongside their command/query
- MediatR pipeline: `LoggingBehavior → ValidationBehavior → CachingBehavior → PerfBehavior → Handler`

### Event-Driven Integration

Services communicate via RabbitMQ + MassTransit. Events (`ProductScrapedEvent`, `MatchCreatedEvent`, `OpportunityScoredEvent`, etc.) are published by services and consumed by downstream workers. Each service configures its own consumers in `Program.cs`.

### Database Strategy

Each service has its own schema/database in MySQL (separate `cma_products`, `cma_matching`, `cma_scoring`, `cma_notifications`, `cma_scraping` databases). Infrastructure migrations live in `Common.Infrastructure`.

---

## Key Patterns

- **Aggregate Root**: `Product`, `ProductMatch`, `OpportunityScore`, `Alert` — each owns its consistency boundary
- **Repository**: `IProductRepository`, `IMatchRepository` — abstractions over EF Core, defined in Domain, implemented in Infrastructure
- **Specification**: Reusable query filters (e.g., `ConfirmedMatchSpecification`)
- **Factory**: `OpportunityScoreFactory.Create()` for complex object construction
- **Value Objects**: `Money` (amount + currency), `Percentage` — immutable, equality by value
- **Polly**: Retry and circuit breaker on RabbitMQ, database, and external API calls

---

## Data Flow

```
ScrapingService.Worker (scheduled jobs)
  → scrapes Amazon/Walmart/Shopee
  → ProductService (saves product + price snapshot)
  → publishes ProductScrapedEvent
  → ScrapingService.Worker (LandedCostRecalcJob)
  → ScoringService (saves opportunity score)
  → publishes MatchCreatedEvent / OpportunityScoredEvent
  → NotificationService (checks thresholds, sends alerts)
  → CMA.Gateway → React SPA (polling + WebSocket)
```

---

## Important Conventions

- **`dotnet format`** is not used — code style is enforced by editor config only
- Migrations are added via `dotnet ef` CLI targeting `Common.Infrastructure` with the API project as startup
- Each service's `Program.cs` registers its own DI: domain interfaces → infrastructure implementations
- ScrapingService has no HTTP API — it's purely a `BackgroundService` / Quartz.NET worker
- Frontend is in `src/Apps/CMA.WebApp` (React + Vite) but is currently scaffolded; main entry is via gateway on port 8080
