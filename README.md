# CrossMarket Price Analyzer

**U.S. → Vietnam Cross-Border Price Comparison & Opportunity Discovery Tool**

> **Status:** Phase 0 — Foundation (In Progress) | **Version:** 1.0 | **Target MVP:** July 2026

---

## 🎯 Overview

A web-based platform that identifies profitable cross-border trade opportunities between U.S. and Vietnam markets. The system continuously scrapes prices from U.S. retail/wholesale sources and Vietnamese e-commerce platforms, normalizes data across currencies and packaging formats, calculates fully-loaded landed costs, and ranks products by profit margin potential.

## 🏗️ Architecture

```
Client (React SPA)
       │ REST / WebSocket
       ▼
  API Gateway (YARP)
       │
  ┌────┴────┬─────────┬──────────────┐
  │          │         │              │
Product  Matching  Scoring    Notification
Service   Service   Service      Service
  │          │         │              │
  └──────────┴────┬────┴──────────────┘
                  │ RabbitMQ
                  ▼
         ┌─────────────────┐
         │  Background      │
         │  Workers        │
         └────────┬────────┘
                  │
  ┌───────────────┼───────────────────┐
  │               │                   │
MySQL 8.0    Redis 7         Elasticsearch
```

## 📁 Project Structure

```
CrossMarketAnalyzer/
├── src/
│   ├── Common/
│   │   ├── Common.Domain/          # Shared kernel
│   │   ├── Common.Application/     # Pipeline behaviors
│   │   └── Common.Infrastructure/  # EF Core, Redis, RabbitMQ, Serilog
│   ├── Services/
│   │   ├── ProductService/         # Product catalog + price snapshots
│   │   ├── MatchingService/        # Fuzzy matching engine
│   │   ├── ScoringService/         # Multi-factor scoring
│   │   ├── NotificationService/   # Alerts + delivery
│   │   └── ScrapingService/       # Background workers
│   └── Apps/
│       ├── CMA.Gateway/            # YARP API Gateway
│       └── CMA.WebApp/             # React SPA
├── tests/
│   ├── Common.UnitTests/
│   ├── ProductService.UnitTests/
│   ├── MatchingService.UnitTests/
│   ├── ScoringService.UnitTests/
│   └── ProductService.IntegrationTests/
├── docs/
├── docker-compose.yml
└── CrossMarketAnalyzer.sln
```

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Node.js 20+](https://nodejs.org/)

### 1. Start Infrastructure

```bash
docker compose up -d mysql redis rabbitmq
```

### 2. Run Backend

```bash
dotnet restore CrossMarketAnalyzer.sln
dotnet build CrossMarketAnalyzer.sln
dotnet run --project src/Services/ProductService/ProductService.Api
```

### 3. Run Frontend

```bash
cd src/Apps/CMA.WebApp
npm install
npm run dev
```

### 4. Access Application

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Gateway | http://localhost:8080 |
| Gateway Health | http://localhost:8080/health |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| API (via Gateway) | http://localhost:8080/api/products |

## 🔧 Development

### Build

```bash
dotnet build CrossMarketAnalyzer.sln --configuration Release
```

### Test

```bash
dotnet test CrossMarketAnalyzer.sln --configuration Release
```

### Database Migrations

```bash
dotnet ef migrations add InitialCreate \
  --project src/Common/Common.Infrastructure \
  --startup-project src/Services/ProductService/ProductService.Api

dotnet ef database update \
  --project src/Common/Common.Infrastructure \
  --startup-project src/Services/ProductService/ProductService.Api
```

## 📊 Tech Stack

| Layer | Technology |
|---|---|
| Frontend | React 18, Vite 5, Tailwind CSS, shadcn/ui, React Query, Zustand |
| API Gateway | YARP (.NET) |
| Backend | .NET 9, CQRS, MediatR, EF Core |
| Database | MySQL 8.0 |
| Cache | Redis 7 |
| Messaging | RabbitMQ 3.12, MassTransit |
| Logging | Serilog → Elasticsearch |
| Observability | OpenTelemetry, Prometheus, Grafana |
| CI/CD | GitHub Actions |
| Container | Docker, Docker Compose |

## 📅 Roadmap

See [documents/ROADMAP.md](documents/ROADMAP.md) for the full 30-week implementation plan.

| Milestone | Target | Scope |
|---|---|---|
| **MVP v1.0** | Week 14 (July 2026) | Core scraping + matching + dashboard |
| **v1.5** | Week 22 (Sept 2026) | Multi-source APIs + shipping integrations |
| **v2.0** | Week 30 (Nov 2026) | Alerts, auth, reports, watchlists |

## 📄 Documents

| Document | Description |
|---|---|
| `documents/CrossMarket_Price_Analyzer_PRD.md` | Product Requirements Document |
| `documents/ARCHITECTURE_OVERVIEW.md` | System-level architecture |
| `documents/BACKEND_DESIGN.md` | .NET backend design (DDD + CQRS) |
| `documents/FRONTEND_DESIGN.md` | React SPA design |
| `documents/ROADMAP.md` | 30-week implementation plan |
| `docs/ARCHITECTURE_DIAGRAM.md` | Visual architecture diagrams |

## 📜 License

Internal / Confidential — All rights reserved.
