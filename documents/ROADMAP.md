# PROJECT ROADMAP
## CrossMarket Price Analyzer

**Version:** 1.0 | **Date:** April 3, 2026 | **Status:** Draft

---

## Table of Contents
1. [Overview](#1-overview)
2. [Phase 0 — Foundation (Weeks 1–3)](#2-phase-0--foundation-weeks-13)
3. [Phase 1 — MVP Core (Weeks 4–10)](#3-phase-1--mvp-core-weeks-410)
4. [Phase 2 — MVP Completion & Polish (Weeks 11–14)](#4-phase-2--mvp-completion--polish-weeks-1114)
5. [Phase 3 — v1.5 Feature Expansion (Weeks 15–22)](#5-phase-3--v15-feature-expansion-weeks-1522)
6. [Phase 4 — v2.0 Full Platform (Weeks 23–30)](#6-phase-4--v20-full-platform-weeks-2330)
7. [Milestone Summary](#7-milestone-summary)
8. [Risk-Adjusted Schedule Buffer](#8-risk-adjusted-schedule-buffer)

---

## 1. Overview

This roadmap maps the PRD requirements to 4 execution phases spanning ~30 weeks (Q2–Q4 2026), targeting:

| Release | Target | Scope |
|---|---|---|
| **MVP (v1.0)** | Week 14 (July 2026) | Core scraping + matching + dashboard |
| **v1.5** | Week 22 (Sept 2026) | Multi-source APIs + shipping integrations |
| **v2.0** | Week 30 (Nov 2026) | Alerts, auth, reports, watchlists |

### Resource Assumptions
- **Team:** 4 developers (2 Backend, 1 Frontend, 1 DevOps/Fullstack)
- **Working Model:** Full-time on the project
- **Tech Experience:** .NET 9, React 18, MySQL — team familiar

### Dependency Notes
- Shopee/Lazada Open Platform API approval is an external dependency (assume 2–4 week lead time)
- Legal review of scraping compliance should run in parallel during Phase 0

---

## 2. Phase 0 — Foundation (Weeks 1–3)

**Goal:** Set up all infrastructure, tooling, CI/CD, and architectural scaffolding before writing product code.

### Objectives
- ✅ Working development environment (Docker + all containers)
- ✅ .NET 9 solution structure (Common lib + 5 services)
- ✅ React project scaffolded with Vite
- ✅ GitHub Actions CI pipeline passing
- ✅ Database migrations running
- ✅ Architecture documented and reviewed

| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P0-F01 | Provision Docker Compose environment (MySQL, Redis, RabbitMQ) | DevOps | Infra | 🔴 Critical |
| P0-F02 | Create .NET 9 solution with project structure (Common + 5 services) | Backend | Dev | 🔴 Critical |
| P0-F03 | Implement Common.Domain base classes (BaseEntity, ValueObjects, Enums) | Backend | Dev | 🔴 Critical |
| P0-F04 | Implement Common.Application pipeline behaviors (Validation, Logging, Caching) | Backend | Dev | 🔴 Critical |
| P0-F05 | Configure EF Core with MySQL; write all database migrations | Backend | Dev | 🔴 Critical |
| P0-F06 | Scaffold React 18 project with Vite + Tailwind + shadcn/ui | Frontend | Dev | 🔴 Critical |
| P0-F07 | Configure GitHub Actions CI pipeline (build + test) | DevOps | Infra | 🔴 Critical |
| P0-F08 | Set up YARP API Gateway with routing rules | Backend | Dev | 🔴 Critical |
| P0-F09 | Implement base shared UI components (Button, Card, Input, etc.) | Frontend | Dev | 🔴 High |
| P0-F10 | Configure Serilog + OpenTelemetry | DevOps | Infra | 🟡 Medium |
| P0-F11 | Create system architecture diagram and review | Architect | Docs | 🟡 Medium |
| P0-F12 | Legal review of scraping compliance for target sites | External | Legal | 🟡 Medium |

**Phase 0 Exit Criteria:**
- `docker-compose up` brings up all infrastructure
- `dotnet build` succeeds for all 5 services
- `npm run dev` serves React app
- CI pipeline passes (build + unit tests)
- All database tables exist in MySQL

---

## 3. Phase 1 — MVP Core (Weeks 4–10)

**Goal:** Build the complete data flow from scraping → matching → scoring → dashboard display.

### 3.1 Backend — ProductService (Weeks 4–6)

| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P1-B01 | Implement Product, Brand, Category domain entities | Backend | Dev | 🔴 Critical |
| P1-B02 | Implement PriceSnapshot entity + repository | Backend | Dev | 🔴 Critical |
| P1-B03 | Implement ProductService REST API (list, get, price history) | Backend | Dev | 🔴 Critical |
| P1-B04 | Implement QuickLookupCommand (URL → scrape → analyze) | Backend | Dev | 🔴 High |
| P1-B05 | Implement ExchangeRateService with Redis cache | Backend | Dev | 🔴 High |
| P1-B06 | Implement GetOpportunitiesQuery (aggregates ScoringService data) | Backend | Dev | 🔴 High |
| P1-B07 | Write unit tests for ProductService domain logic | Backend | Test | 🟡 Medium |
| P1-B08 | Write unit tests for command/query handlers | Backend | Test | 🟡 Medium |

### 3.2 Backend — MatchingService (Weeks 5–7)

| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P1-B09 | Implement ProductMatch aggregate + repository | Backend | Dev | 🔴 Critical |
| P1-B10 | Implement FuzzyMatchingService (Levenshtein + TF-IDF) | Backend | Dev | 🔴 Critical |
| P1-B11 | Implement MatchingService REST API (list, confirm, reject) | Backend | Dev | 🔴 Critical |
| P1-B12 | Implement batch confirm/reject endpoint | Backend | Dev | 🟡 Medium |
| P1-B13 | Write unit tests for FuzzyMatchingService | Backend | Test | 🟡 Medium |

### 3.3 Backend — ScoringService (Weeks 6–8)

| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P1-B14 | Implement OpportunityScore aggregate + repository | Backend | Dev | 🔴 Critical |
| P1-B15 | Implement ScoringEngine (weighted multi-factor) | Backend | Dev | 🔴 Critical |
| P1-B16 | Implement LandedCostCalculator (shipping + duty + VAT + handling) | Backend | Dev | 🔴 Critical |
| P1-B17 | Implement ScoringService REST API (scores list, weights config) | Backend | Dev | 🟡 Medium |
| P1-B18 | Write unit tests for ScoringEngine and LandedCostCalculator | Backend | Test | 🟡 Medium |

### 3.4 Backend — Scraping Workers (Weeks 7–9)

| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P1-B19 | Set up Quartz.NET job scheduler in ScrapingService | Backend | Dev | 🔴 Critical |
| P1-B20 | Implement Amazon scraper (Playwright) | Backend | Dev | 🔴 Critical |
| P1-B21 | Implement Walmart scraper (Playwright) | Backend | Dev | 🔴 High |
| P1-B22 | Implement cigarpage.com scraper (Playwright) | Backend | Dev | 🟡 Medium |
| P1-B23 | Implement Shopee API client (or mock if API not yet approved) | Backend | Dev | 🟡 Medium |
| P1-B24 | Implement ExchangeRateUpdateJob (hourly) | Backend | Dev | 🔴 High |
| P1-B25 | Implement CostRecalcJob and ScoringJob (6-hourly) | Backend | Dev | 🟡 Medium |

### 3.5 Frontend — MVP Pages (Weeks 6–10)

| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P1-F01 | Build DashboardPage with OpportunityCard feed | Frontend | Dev | 🔴 Critical |
| P1-F02 | Build ComparisonPage (US vs VN product, cost breakdown, scoring) | Frontend | Dev | 🔴 Critical |
| P1-F03 | Build FilterBar with all dashboard filters | Frontend | Dev | 🔴 High |
| P1-F04 | Build CategoryPage with category grid | Frontend | Dev | 🟡 Medium |
| P1-F05 | Build QuickLookupPage with URL form + results display | Frontend | Dev | 🔴 High |
| P1-F06 | Build PriceDisplay, ScoreGauge, ConfidenceBadge components | Frontend | Dev | 🔴 High |
| P1-F07 | Implement React Query hooks for all API calls | Frontend | Dev | 🔴 Critical |
| P1-F08 | Build Header + Sidebar layout components | Frontend | Dev | 🔴 Critical |
| P1-F09 | Add Zustand stores (scoringStore, filterStore) | Frontend | Dev | 🔴 High |
| P1-F10 | Build ExportButton (CSV export) | Frontend | Dev | 🟡 Medium |
| P1-F11 | Add Skeleton loading states + ErrorBoundary | Frontend | Dev | 🟡 Medium |
| P1-F12 | Responsive layout testing (mobile, tablet, desktop) | Frontend | Test | 🟡 Medium |

### Phase 1 Exit Criteria
- End-to-end data flow works: URL entered → scraped → matched → scored → displayed on dashboard
- Dashboard loads in < 2s (cached data)
- Scoring weights are adjustable via Settings page
- CSV export works

---

## 4. Phase 2 — MVP Completion & Polish (Weeks 11–14)

**Goal:** Fix bugs, add v1.0 edge cases, polish UX, and prepare for first internal release.

### Backend Polish
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P2-B01 | Add Polly resilience (retry + circuit breaker) to all external calls | Backend | Dev | 🔴 Critical |
| P2-B02 | Implement outbox pattern for reliable event publishing (v1.5 ready) | Backend | Dev | 🟡 Medium |
| P2-B03 | Write integration tests (Testcontainers: MySQL + Redis + RabbitMQ) | Backend | Test | 🟡 Medium |
| P2-B04 | Optimize database queries (add missing indexes, N+1 fixes) | Backend | Dev | 🟡 Medium |
| P2-B05 | Add RotatingProxyService for scrapers (IP rotation, rate limiting) | Backend | Dev | 🟡 Medium |
| P2-B06 | Implement manual shipping/duty cost input (override auto-estimate) | Backend | Dev | 🟡 Medium |
| P2-B07 | Legal review: final scraping compliance sign-off | External | Legal | 🟡 Medium |

### Frontend Polish
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P2-F01 | Build PriceHistoryPage with Recharts line chart + date picker | Frontend | Dev | 🔴 High |
| P2-F02 | Build AlertsPage with notification list | Frontend | Dev | 🟡 Medium |
| P2-F03 | Build SettingsPage with scoring weight sliders | Frontend | Dev | 🔴 High |
| P2-F04 | Internationalization: translate all strings (EN + VI) | Frontend | Dev | 🟡 Medium |
| P2-F05 | Accessibility audit (axe-core) + fixes | Frontend | QA | 🟡 Medium |
| P2-F06 | E2E tests: Dashboard, Quick Lookup, Comparison (Playwright) | QA | Test | 🟡 Medium |
| P2-F07 | Performance optimization (lazy load routes, React.memo cards) | Frontend | Dev | 🟡 Medium |
| P2-F08 | Empty states + error states for all lists and forms | Frontend | Dev | 🟡 Medium |

### Deployment & Documentation
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P2-D01 | Configure production Docker Compose with health checks | DevOps | Infra | 🔴 Critical |
| P2-D02 | Configure GitHub Actions CD pipeline (auto-deploy to staging) | DevOps | Infra | 🔴 Critical |
| P2-D03 | Set up Prometheus + Grafana monitoring dashboards | DevOps | Infra | 🟡 Medium |
| P2-D04 | Write API documentation (Swagger/OpenAPI) for all endpoints | Backend | Docs | 🟡 Medium |
| P2-D05 | Write user guide (markdown) for MVP features | Scribe | Docs | 🟡 Medium |
| P2-D06 | Internal user acceptance testing (UAT) | Team | QA | 🔴 Critical |

**Phase 2 Exit Criteria:** MVP v1.0 released to internal team for validation.

---

## 5. Phase 3 — v1.5 Feature Expansion (Weeks 15–22)

**Goal:** Expand data sources, integrate shipping APIs, add HS code detection, and improve export capabilities.

### Backend v1.5
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P3-B01 | Implement Lazada Open Platform API client | Backend | Dev | 🔴 High |
| P3-B02 | Implement Tiki scraping (unofficial API) | Backend | Dev | 🟡 Medium |
| P3-B03 | Integrate FedEx shipping API for automated estimates | Backend | Dev | 🔴 High |
| P3-B04 | Integrate DHL shipping API (fallback + comparison) | Backend | Dev | 🟡 Medium |
| P3-B05 | Implement HS code auto-detection from product name | Backend | Dev | 🟡 Medium |
| P3-B06 | Update tariff tables (monthly refresh) | Backend | Dev | 🟡 Medium |
| P3-B07 | Implement Excel export with formatted sheets (EPPlus) | Backend | Dev | 🟡 Medium |
| P3-B08 | Price stability calculation (rolling 30-day variance) | Backend | Dev | 🟡 Medium |
| P3-B09 | WebSocket server for real-time opportunity push | Backend | Dev | 🟡 Medium |

### Frontend v1.5
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P3-F01 | WebSocket integration for real-time opportunity updates | Frontend | Dev | 🟡 Medium |
| P3-F02 | MarginBarChart (margin comparison across products) | Frontend | Dev | 🟡 Medium |
| P3-F03 | ScoreRadarChart (multi-factor breakdown) | Frontend | Dev | 🟡 Medium |
| P3-F04 | Export: Excel format (SheetJS) | Frontend | Dev | 🟡 Medium |
| P3-F05 | Filter: Source marketplace multi-select (Shopee, Lazada, Tiki) | Frontend | Dev | 🟡 Medium |
| P3-F06 | Dashboard: Top movers / biggest margin changes | Frontend | Dev | 🟡 Medium |

### Testing & Infrastructure
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P3-T01 | Integration tests for all API endpoints | Backend | Test | 🟡 Medium |
| P3-T02 | Load testing (k6) — 50,000 product dataset | DevOps | Test | 🟡 Medium |
| P3-T03 | API documentation via Swagger (re-generated) | Backend | Docs | 🟡 Medium |
| P3-T04 | Deploy to production environment | DevOps | Infra | 🔴 High |

**Phase 3 Exit Criteria:** v1.5 released with all Phase 3 tasks completed.

---

## 6. Phase 4 — v2.0 Full Platform (Weeks 23–30)

**Goal:** Complete platform with user accounts, alerting, reports, and PDF export.

### Backend v2.0
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P4-B01 | Implement user authentication (JWT + Identity) | Backend | Dev | 🔴 Critical |
| P4-B02 | Implement user registration + login APIs | Backend | Dev | 🔴 Critical |
| P4-B03 | Implement saved product watchlist | Backend | Dev | 🔴 High |
| P4-B04 | Implement NotificationService REST API (alerts + subscriptions) | Backend | Dev | 🔴 High |
| P4-B05 | Implement email notifications (SendGrid) | Backend | Dev | 🟡 Medium |
| P4-B06 | Implement Telegram bot notifications | Backend | Dev | 🟡 Medium |
| P4-B07 | Implement alert threshold engine (check thresholds on every score) | Backend | Dev | 🔴 High |
| P4-B08 | Implement scheduled report generation | Backend | Dev | 🟡 Medium |
| P4-B09 | Implement PDF report generation (QuestPDF) | Backend | Dev | 🟡 Medium |
| P4-B10 | Implement multi-tenancy (each user sees own alerts + watchlists) | Backend | Dev | 🟡 Medium |

### Frontend v2.0
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P4-F01 | Login / Register page + JWT auth flow | Frontend | Dev | 🔴 Critical |
| P4-F02 | Protect routes (redirect unauthenticated users) | Frontend | Dev | 🔴 Critical |
| P4-F03 | User profile page | Frontend | Dev | 🟡 Medium |
| P4-F04 | Add to Watchlist button on all opportunity cards | Frontend | Dev | 🟡 Medium |
| P4-F05 | My Watchlist page | Frontend | Dev | 🟡 Medium |
| P4-F06 | Alert preferences UI (email/Telegram/InApp toggles) | Frontend | Dev | 🟡 Medium |
| P4-F07 | PDF export from frontend | Frontend | Dev | 🟡 Medium |
| P4-F08 | Scheduled reports UI (select frequency + products) | Frontend | Dev | 🟡 Medium |

### Testing & Launch
| # | Task | Owner | Type | Priority |
|---|---|---|---|---|
| P4-T01 | Security audit (OWASP Top 10 check) | Security | Audit | 🔴 Critical |
| P4-T02 | Full regression test suite (Playwright) | QA | Test | 🟡 Medium |
| P4-T03 | Penetration testing | External | Security | 🟡 Medium |
| P4-T04 | Performance benchmark at 50,000 products | DevOps | Test | 🟡 Medium |
| P4-T05 | Write v2.0 user documentation | Scribe | Docs | 🟡 Medium |
| P4-T06 | Production deployment + DNS + SSL | DevOps | Infra | 🔴 Critical |
| P4-T07 | Soft launch to first external users | Team | Launch | 🔴 Critical |

**Phase 4 Exit Criteria:** v2.0 launched to first external beta users.

---

## 7. Milestone Summary

```
Week  1 ───────────────────────────────────────────────────── Week 30
        ├──P0──┤
               ├──P1────────────────────────────────┤
                                              ├──P2─┤
                                                     ├──P3──┤
                                                              ├──P4──┤

  MVP v1.0 ────────────────────────────────────────────────── v1.5 ─── v2.0
  Week 14                                                           Week 22   Week 30
```

| Milestone | Target Date | Key Deliverables |
|---|---|---|
| **M1: Foundation** | Week 3 | Infra ready; solution scaffolded; CI passing |
| **M2: Alpha** | Week 8 | Scraping → Matching → Scoring data flow works end-to-end |
| **M3: MVP v1.0** | Week 14 | Dashboard, comparison, quick lookup, CSV export |
| **M4: v1.5** | Week 22 | Lazada/Tiki APIs, shipping integrations, Excel export |
| **M5: v2.0** | Week 30 | Auth, alerts, PDF reports, multi-tenancy |

---

## 8. Risk-Adjusted Schedule Buffer

Each phase includes a 2-week implicit buffer for:
- Unexpected scraper breakage (website structure changes)
- API approval delays (Shopee/Lazada)
- Complex edge cases discovered during development
- Legal/compliance blockers

**Total estimated duration:** 30 weeks (≈ 7.5 months)
**Target release date:** November 2026
