# BACKEND DESIGN DOCUMENT
## CrossMarket Price Analyzer — .NET 9.0 Microservices with DDD

**Version:** 1.0 | **Date:** April 3, 2026 | **Status:** Draft

---

## Table of Contents
1. [Design Principles & Architecture](#1-design-principles--architecture)
2. [Solution Structure](#2-solution-structure)
3. [Microservices Detail](#3-microservices-detail)
4. [Domain Model](#4-domain-model)
5. [CQRS & Mediator Pattern](#5-cqrs--mediator-pattern)
6. [API Contracts](#6-api-contracts)
7. [Database Schema (MySQL)](#7-database-schema-mysql)
8. [Messaging & Integration](#8-messaging--integration)
9. [Design Patterns Applied](#9-design-patterns-applied)
10. [Project Checklist](#10-project-checklist)

---

## 1. Design Principles & Architecture

### Core Principles
- **DDD (Domain-Driven Design):** Each microservice owns its domain model; bounded contexts are strict
- **CQRS:** Separate read (queries) and write (commands) paths for performance and clarity
- **Mediator Pattern:** Use MediatR to decouple command/query handlers from controllers
- **Repository Pattern:** Abstractions over data access with EF Core implementations
- **Dependency Injection:** .NET built-in DI for all service registrations
- **Fail-Fast:** Validate inputs early; throw meaningful domain exceptions
- **12-Factor App:** Config via environment variables; stateless services; disposable resources

### Why DDD + Microservices?

| Concern | Solution |
|---|---|
| **Domain isolation** | Each service owns its bounded context — no shared domain models across services |
| **Independent deployability** | Services can be updated/released independently |
| **Scalability** | Scraping workers scale separately from API servers |
| **Maintainability** | Clean layers mean new developers understand one service at a time |
| **Testability** | Domain logic has no infrastructure dependencies — easy to unit test |

---

## 2. Solution Structure

```
CrossMarketAnalyzer/
│
├── src/
│   ├── Common/
│   │   ├── Common.Domain/               # Shared kernel (base entities, value objects, enums)
│   │   │   ├── Entities/                # BaseEntity<TId>, AuditableEntity
│   │   │   ├── ValueObjects/            # Money, Percentage, DateRange, CountryCode
│   │   │   ├── Enums/                   # ProductSource, MatchStatus, AlertType
│   │   │   ├── Exceptions/              # DomainExceptions, ApplicationExceptions
│   │   │   └── Interfaces/              # IRepository<T>, IUnitOfWork
│   │   │
│   │   ├── Common.Application/           # Shared behaviors (logging, validation, perf)
│   │   │   ├── Behaviors/               # LoggingBehavior, ValidationBehavior, PerfBehavior
│   │   │   ├── Validators/               # Shared FluentValidation validators
│   │   │   ├── Interfaces/               # ICacheService, IEventPublisher, ICurrentUser
│   │   │   └── Extensions/               # ServiceCollection extensions
│   │   │
│   │   └── Common.Infrastructure/        # Shared infrastructure
│   │       ├── Persistence/              # BaseDbContext, config conventions
│   │       ├── Caching/                   # RedisCacheService
│   │       ├── Messaging/                 # RabbitMqEventPublisher
│   │       └── Logging/                   # Serilog configuration
│   │
│   ├── Services/
│   │   ├── ProductService/
│   │   │   ├── ProductService.Domain/
│   │   │   ├── ProductService.Application/
│   │   │   ├── ProductService.Infrastructure/
│   │   │   └── ProductService.Api/        # Program.cs, Controllers
│   │   │
│   │   ├── MatchingService/
│   │   │   ├── MatchingService.Domain/
│   │   │   ├── MatchingService.Application/
│   │   │   ├── MatchingService.Infrastructure/
│   │   │   └── MatchingService.Api/
│   │   │
│   │   ├── ScoringService/
│   │   │   ├── ScoringService.Domain/
│   │   │   ├── ScoringService.Application/
│   │   │   ├── ScoringService.Infrastructure/
│   │   │   └── ScoringService.Api/
│   │   │
│   │   ├── NotificationService/
│   │   │   ├── NotificationService.Domain/
│   │   │   ├── NotificationService.Application/
│   │   │   ├── NotificationService.Infrastructure/
│   │   │   └── NotificationService.Api/
│   │   │
│   │   └── ScrapingService/
│   │       ├── ScrapingService.Domain/
│   │       ├── ScrapingService.Application/
│   │       └── ScrapingService.Worker/    # Background service (no HTTP)
│   │
│   └── Apps/
│       ├── CMA.Gateway/                  # YARP API Gateway
│       └── CMA.WebApp/                   # React SPA (served as static files)
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

## 3. Microservices Detail

### 3.1 ProductService

**Responsibility:** Master product catalog, price snapshot management, exchange rate caching, product normalization.

**Public APIs:**

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/products` | List products (paginated, filterable) |
| GET | `/api/products/{id}` | Get product by ID |
| GET | `/api/products/{id}/prices` | Get price history for a product |
| POST | `/api/products/quick-lookup` | Analyze a U.S. product URL |
| GET | `/api/categories` | List all HS code categories |
| GET | `/api/exchange-rates/current` | Get current USD→VND rate |
| GET | `/api/opportunities` | List ranked opportunities (with filters) |

**Key Domain Entities:** `Product`, `PriceSnapshot`, `Category`, `ExchangeRate`

### 3.2 MatchingService

**Responsibility:** Pair U.S. products with Vietnam marketplace listings using fuzzy matching; manage match confirmation workflow.

**Public APIs:**

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/matches` | List product matches (paginated) |
| GET | `/api/matches/{id}` | Get match detail with U.S. ↔ VN comparison |
| POST | `/api/matches/{id}/confirm` | User confirms a match |
| POST | `/api/matches/{id}/reject` | User rejects a match |
| POST | `/api/matches/batch-review` | Batch confirm/reject |

**Key Domain Entities:** `ProductMatch`, `MatchConfirmation`, `ConfidenceScore`

### 3.3 ScoringService

**Responsibility:** Calculate landed costs, apply multi-factor scoring, rank opportunities.

**Public APIs:**

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/scores` | List all opportunity scores (ranked) |
| GET | `/api/scores/{matchId}` | Get scoring breakdown for a match |
| PUT | `/api/scores/weights` | Update scoring factor weights |
| GET | `/api/scores/config` | Get current scoring weights |
| POST | `/api/scores/recalculate` | Trigger manual recalculation |

**Key Domain Entities:** `OpportunityScore`, `ScoringFactors`, `ScoringConfig`

### 3.4 NotificationService

**Responsibility:** Manage user alerts, subscriptions, and multi-channel delivery.

**Public APIs:**

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/alerts` | List user alerts |
| PUT | `/api/alerts/{id}/read` | Mark alert as read |
| DELETE | `/api/alerts/{id}` | Delete alert |
| GET | `/api/subscriptions` | List user subscriptions |
| POST | `/api/subscriptions` | Create new alert subscription |
| PUT | `/api/subscriptions/{id}` | Update subscription |
| DELETE | `/api/subscriptions/{id}` | Delete subscription |

**Key Domain Entities:** `Alert`, `Subscription`, `DeliveryChannel` (Email/Telegram/InApp)

### 3.5 ScrapingService (Worker)

**Responsibility:** Background scraping workers — no HTTP API exposed. Runs on Quartz.NET schedule.

**Jobs:**
1. `UsProductScrapingJob` — Daily, scrapes U.S. sources (Amazon, Walmart, cigarpage.com)
2. `VnProductScrapingJob` — Daily, calls Shopee/Lazada APIs
3. `ExchangeRateUpdateJob` — Hourly, fetches USD→VND rate
4. `LandedCostRecalcJob` — Every 6 hours, recalculates all active landed costs
5. `ScoringJob` — Every 6 hours, re-applies scoring to all confirmed matches

---

## 4. Domain Model

### 4.1 Product Aggregate (`ProductService.Domain`)

```
Product (Aggregate Root)
├── Id: Guid
├── Name: string
├── Brand: Brand (entity)
├── Sku: string?
├── Category: Category (entity)
├── HsCode: string?
├── Source: ProductSource (enum: Amazon, Walmart, Shopee, Lazada, Tiki)
├── SourceUrl: string
├── IsActive: bool
├── PriceSnapshots: ICollection<PriceSnapshot>
└── CreatedAt, UpdatedAt

PriceSnapshot (Entity)
├── Id: Guid
├── ProductId: Guid (FK)
├── Price: Money (value object)
├── UnitPrice: Money
├── QuantityPerUnit: decimal
├── SellerName: string?
├── SellerRating: decimal?
├── SalesVolume: int?
└── ScrapedAt: DateTime

Brand (Entity)
├── Id: Guid
├── Name: string
└── NormalizedName: string

Category (Entity)
├── Id: Guid
├── Name: string
├── HsCode: string
└── ParentCategoryId: Guid?
```

### 4.2 Match Aggregate (`MatchingService.Domain`)

```
ProductMatch (Aggregate Root)
├── Id: Guid
├── UsProductId: Guid
├── VnProductId: Guid
├── ConfidenceScore: ConfidenceLevel (value object — enum: Low/Medium/High)
├── Score: decimal (0–100)
├── Status: MatchStatus (enum: Pending/Confirmed/Rejected)
├── ConfirmedBy: string?
├── ConfirmedAt: DateTime?
└── CreatedAt

MatchConfirmation (Entity — part of aggregate)
├── Id: Guid
├── MatchId: Guid (FK)
├── UserId: string
├── Action: ConfirmAction (enum: Confirmed/Rejected)
└── Notes: string?
```

### 4.3 Score Aggregate (`ScoringService.Domain`)

```
OpportunityScore (Aggregate Root)
├── Id: Guid
├── MatchId: Guid
├── Factors: ScoringFactors (value object)
│   ├── ProfitMarginPercent: decimal (weight: 40%)
│   ├── MarketDemandScore: decimal (weight: 25%)
│   ├── CompetitionScore: decimal (weight: 20%)
│   ├── PriceStabilityScore: decimal (weight: 10%)
│   └── MatchConfidenceScore: decimal (weight: 5%)
├── CompositeScore: decimal (0–100)
├── LandedCost: Money
├── VietnamRetailPrice: Money
├── PriceDifference: Money
├── ProfitMargin: Percentage
├── Roi: Percentage
└── CalculatedAt: DateTime

ScoringConfig (Entity)
├── Id: Guid
├── FactorKey: string
├── Weight: decimal
├── MinThreshold: decimal
├── MaxThreshold: decimal
└── IsActive: bool
```

### 4.4 Alert Aggregate (`NotificationService.Domain`)

```
Alert (Aggregate Root)
├── Id: Guid
├── UserId: string
├── MatchId: Guid?
├── Type: AlertType (enum: NewOpportunity/PriceDrop/Threshold/MatchConfirmation)
├── Title: string
├── Message: string
├── IsRead: bool
├── ReadAt: DateTime?
└── CreatedAt: DateTime

Subscription (Aggregate Root)
├── Id: Guid
├── UserId: string
├── MatchId: Guid?
├── Channel: DeliveryChannel (enum: Email/Telegram/InApp)
├── ThresholdMargin: decimal?
├── IsActive: bool
└── CreatedAt
```

---

## 5. CQRS & Mediator Pattern

### Command / Query Separation

Every API action is either a **Command** (write) or **Query** (read):

```
ProductService.Application/
├── Commands/
│   ├── CreateProductCommand.cs      → IRequest<Guid>
│   ├── UpdateProductCommand.cs      → IRequest<bool>
│   ├── QuickLookupCommand.cs         → IRequest<QuickLookupResultDto>
│   └── Handlers/
│       ├── CreateProductHandler.cs
│       └── QuickLookupHandler.cs
│
├── Queries/
│   ├── GetProductByIdQuery.cs        → IRequest<ProductDto>
│   ├── GetOpportunitiesQuery.cs      → IRequest<PaginatedResult<OpportunityDto>>
│   ├── GetPriceHistoryQuery.cs       → IRequest<IReadOnlyList<PriceHistoryDto>>
│   └── Handlers/
│       ├── GetOpportunitiesHandler.cs  # Reads from ScoringService via HTTP
│       └── GetPriceHistoryHandler.cs
│
└── DTOs/
    ├── ProductDto.cs
    ├── OpportunityDto.cs
    └── PriceHistoryDto.cs
```

### Pipeline Behaviors (Cross-Cutting)

```csharp
// Applied to ALL requests via DI registration
pipeline.Use<LoggingBehavior>();   // Log request/response with timing
pipeline.Use<ValidationBehavior>(); // FluentValidation before handler
pipeline.Use<CachingBehavior>();    // Cache query results in Redis
pipeline.Use<PerfBehavior>();       // Track metrics per request
```

---

## 6. API Contracts

### Core DTOs (ProductService)

```csharp
// GET /api/products
public record ProductDto(
    Guid Id,
    string Name,
    string BrandName,
    string? Sku,
    string CategoryName,
    string? HsCode,
    string Source,
    string SourceUrl,
    MoneyDto? LatestPrice,
    DateTime CreatedAt
);

// GET /api/opportunities
public record OpportunityDto(
    Guid MatchId,
    ProductDto UsProduct,
    ProductDto VnProduct,
    decimal CompositeScore,
    decimal ProfitMarginPercent,
    decimal RoiPercent,
    MoneyDto LandedCost,
    MoneyDto VietnamRetailPrice,
    MoneyDto PriceDifference,
    decimal DemandScore,
    decimal CompetitionScore,
    DateTime CalculatedAt
);

// POST /api/products/quick-lookup
public record QuickLookupRequest(string ProductUrl);
public record QuickLookupResultDto(
    ProductDto Product,
    List<OpportunityDto> Opportunities,
    decimal EstimatedLandedCost,
    string Summary
);
```

### Landed Cost DTO

```csharp
public record LandedCostBreakdownDto(
    Guid MatchId,
    MoneyDto UsPurchasePrice,      // Original USD
    MoneyDto UsPurchasePriceVnd,  // Converted to VND
    MoneyDto ShippingCost,
    MoneyDto ImportDuty,
    MoneyDto Vat,
    MoneyDto HandlingFees,
    MoneyDto TotalLandedCost
);
```

---

## 7. Database Schema (MySQL)

### Tables

```sql
-- products
CREATE TABLE products (
    id CHAR(36) PRIMARY KEY,
    name VARCHAR(500) NOT NULL,
    brand_id CHAR(36),
    sku VARCHAR(100),
    category_id CHAR(36),
    hs_code VARCHAR(20),
    source ENUM('Amazon','Walmart','Shopee','Lazada','Tiki','CigarPage','Manual') NOT NULL,
    source_url VARCHAR(2000) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    INDEX idx_source (source),
    INDEX idx_brand (brand_id),
    INDEX idx_category (category_id),
    INDEX idx_hs_code (hs_code),
    FULLTEXT idx_name (name)
);

-- price_snapshots
CREATE TABLE price_snapshots (
    id CHAR(36) PRIMARY KEY,
    product_id CHAR(36) NOT NULL,
    price DECIMAL(18,4) NOT NULL,
    currency ENUM('USD','VND') NOT NULL,
    unit_price DECIMAL(18,4) NOT NULL,
    quantity_per_unit DECIMAL(10,2) NOT NULL DEFAULT 1,
    seller_name VARCHAR(200),
    seller_rating DECIMAL(3,2),
    sales_volume INT,
    scraped_at DATETIME(6) NOT NULL,
    FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE,
    INDEX idx_product_time (product_id, scraped_at DESC)
);

-- product_matches
CREATE TABLE product_matches (
    id CHAR(36) PRIMARY KEY,
    us_product_id CHAR(36) NOT NULL,
    vn_product_id CHAR(36) NOT NULL,
    confidence_score DECIMAL(5,2) NOT NULL,
    status ENUM('Pending','Confirmed','Rejected') DEFAULT 'Pending',
    confirmed_by VARCHAR(100),
    confirmed_at DATETIME(6),
    created_at DATETIME(6) NOT NULL,
    FOREIGN KEY (us_product_id) REFERENCES products(id),
    FOREIGN KEY (vn_product_id) REFERENCES products(id),
    INDEX idx_status (status),
    INDEX idx_us_product (us_product_id),
    INDEX idx_confidence (confidence_score)
);

-- opportunity_scores
CREATE TABLE opportunity_scores (
    id CHAR(36) PRIMARY KEY,
    match_id CHAR(36) NOT NULL UNIQUE,
    profit_margin_pct DECIMAL(8,4) NOT NULL,
    demand_score DECIMAL(5,2) NOT NULL,
    competition_score DECIMAL(5,2) NOT NULL,
    price_stability_score DECIMAL(5,2) NOT NULL,
    match_confidence_score DECIMAL(5,2) NOT NULL,
    composite_score DECIMAL(5,2) NOT NULL,
    landed_cost_vnd DECIMAL(18,4) NOT NULL,
    vietnam_retail_vnd DECIMAL(18,4) NOT NULL,
    price_difference_vnd DECIMAL(18,4) NOT NULL,
    calculated_at DATETIME(6) NOT NULL,
    FOREIGN KEY (match_id) REFERENCES product_matches(id) ON DELETE CASCADE,
    INDEX idx_composite (composite_score DESC)
);

-- exchange_rates
CREATE TABLE exchange_rates (
    id CHAR(36) PRIMARY KEY,
    from_currency CHAR(3) NOT NULL,
    to_currency CHAR(3) NOT NULL,
    rate DECIMAL(18,8) NOT NULL,
    fetched_at DATETIME(6) NOT NULL,
    UNIQUE KEY uk_pair (from_currency, to_currency),
    INDEX idx_fetched (fetched_at DESC)
);

-- alerts
CREATE TABLE alerts (
    id CHAR(36) PRIMARY KEY,
    user_id VARCHAR(100) NOT NULL,
    match_id CHAR(36),
    type ENUM('NewOpportunity','PriceDrop','Threshold','MatchConfirmation') NOT NULL,
    title VARCHAR(500) NOT NULL,
    message TEXT NOT NULL,
    is_read BOOLEAN DEFAULT FALSE,
    read_at DATETIME(6),
    created_at DATETIME(6) NOT NULL,
    INDEX idx_user_unread (user_id, is_read),
    INDEX idx_created (created_at DESC)
);

-- subscriptions
CREATE TABLE subscriptions (
    id CHAR(36) PRIMARY KEY,
    user_id VARCHAR(100) NOT NULL,
    match_id CHAR(36),
    channel ENUM('Email','Telegram','InApp') NOT NULL,
    threshold_margin DECIMAL(8,4),
    is_active BOOLEAN DEFAULT TRUE,
    created_at DATETIME(6) NOT NULL,
    INDEX idx_user_active (user_id, is_active)
);

-- scrape_jobs
CREATE TABLE scrape_jobs (
    id CHAR(36) PRIMARY KEY,
    source VARCHAR(50) NOT NULL,
    job_type VARCHAR(100) NOT NULL,
    status ENUM('Scheduled','Running','Completed','Failed') DEFAULT 'Scheduled',
    started_at DATETIME(6),
    completed_at DATETIME(6),
    items_scraped INT DEFAULT 0,
    error_message TEXT,
    INDEX idx_status (status),
    INDEX idx_source (source)
);
```

---

## 8. Messaging & Integration

### MassTransit / RabbitMQ Configuration

```csharp
// Each service configures consumers in Program.cs
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductScrapedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"]);
        cfg.ReceiveEndpoint("product-scraped", e =>
        {
            e.ConfigureConsumer<ProductScrapedConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30)));
        });
    });
});
```

### Events Published

| Event | Assembly | When |
|---|---|---|
| `ProductScrapedEvent` | ProductService | New price snapshot saved |
| `LandedCostCalculatedEvent` | ScrapingService.Worker | Cost recalc complete |
| `MatchCreatedEvent` | MatchingService | New match created |
| `OpportunityScoredEvent` | ScoringService | Score calculated |
| `AlertTriggeredEvent` | NotificationService | Alert sent |

---

## 9. Design Patterns Applied

| Pattern | Where | Benefit |
|---|---|---|
| **Aggregate Root** | Product, ProductMatch, OpportunityScore | Transaction boundary; consistency |
| **Repository** | IProductRepository, IMatchRepository | Abstraction over EF Core |
| **Unit of Work** | EF Core DbContext | Atomic saves within a command |
| **CQRS** | All API endpoints | Clear separation; optimized reads |
| **Mediator / MediatR** | All Commands/Queries | Decoupling; pipeline behaviors |
| **Pipeline Behavior** | Validation, Logging, Caching | Cross-cutting without decorators |
| **Factory** | `OpportunityScoreFactory.Create()` | Complex object construction |
| **Specification** | `ConfirmedMatchSpecification` | Reusable query filters |
| **Outbox Pattern** | Event publishing via DB table | Reliable messaging (in v1.5) |
| **Circuit Breaker** | Polly on external API calls | Graceful degradation |
| **Retry** | Polly on RabbitMQ, database calls | Transient fault handling |
| **Cache-Aside** | Redis cache in query handlers | Performance optimization |

---

## 10. Project Checklist

### Infrastructure Setup
- [ ] Set up Docker & Docker Compose with MySQL, Redis, RabbitMQ
- [ ] Configure Serilog + Elasticsearch for centralized logging
- [ ] Configure OpenTelemetry for distributed tracing
- [ ] Set up GitHub Actions CI/CD pipeline
- [ ] Create .NET 9 solution with 5 service projects + Common library
- [ ] Implement base `BaseEntity`, `AuditableEntity` in Common.Domain
- [ ] Implement value objects: `Money`, `Percentage`, `CountryCode`
- [ ] Set up FluentValidation with shared validators
- [ ] Configure MassTransit + RabbitMQ
- [ ] Configure Redis caching service
- [ ] Implement base `BaseDbContext` with MySQL conventions
- [ ] Set up API Gateway (YARP) with JWT auth

### ProductService
- [ ] Create Product, Brand, Category domain entities
- [ ] Create PriceSnapshot entity with `IProductRepository`
- [ ] Implement `GetProductsQuery` + handler (paginated, filterable)
- [ ] Implement `GetOpportunitiesQuery` (from ScoringService)
- [ ] Implement `QuickLookupCommand` (URL → scrape → analyze)
- [ ] Implement `ExchangeRateService` with Redis caching
- [ ] Create REST API controllers

### MatchingService
- [ ] Create `ProductMatch` aggregate
- [ ] Implement `FuzzyMatchingService` (Levenshtein + TF-IDF)
- [ ] Implement `CreateMatchCommand` + handler
- [ ] Implement `ConfirmMatchCommand` / `RejectMatchCommand`
- [ ] Implement `GetMatchesQuery` (paginated)
- [ ] Publish `MatchCreatedEvent` via MassTransit

### ScoringService
- [ ] Create `OpportunityScore` aggregate with `ScoringFactors`
- [ ] Implement `ScoringEngine` (weighted multi-factor)
- [ ] Implement `LandedCostCalculator` (shipping, duty, VAT, handling)
- [ ] Implement `UpdateWeightsCommand`
- [ ] Expose scoring config API

### NotificationService
- [ ] Create `Alert` and `Subscription` aggregates
- [ ] Implement `EmailNotificationService` (SendGrid / SMTP)
- [ ] Implement `TelegramNotificationService`
- [ ] Implement `AlertSubscriptionService`
- [ ] Consume `OpportunityScoredEvent` and check thresholds

### ScrapingService Worker
- [ ] Set up Quartz.NET job scheduler
- [ ] Implement Playwright scraper for Amazon, Walmart
- [ ] Implement Shopee Open Platform API client
- [ ] Implement Lazada Open Platform API client
- [ ] Implement `UsProductScrapingJob`
- [ ] Implement `ExchangeRateUpdateJob`
- [ ] Implement `LandedCostRecalcJob`

### Testing
- [ ] Unit tests for domain logic (ScoringEngine, LandedCostCalculator, FuzzyMatching)
- [ ] Unit tests for command/query handlers
- [ ] Integration tests using Testcontainers (MySQL, Redis, RabbitMQ)
- [ ] E2E tests with Playwright
