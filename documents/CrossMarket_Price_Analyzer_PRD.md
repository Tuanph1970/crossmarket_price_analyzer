# PRODUCT REQUIREMENTS DOCUMENT

# CrossMarket Price Analyzer

**US – Vietnam Cross-Border Price Comparison & Opportunity Discovery Tool**

---

- **Version:** 1.0
- **Date:** March 30, 2026
- **Status:** Draft
- **Classification:** Internal / Confidential

---

## Table of Contents

1. [Document Overview](#1-document-overview)
2. [Executive Summary](#2-executive-summary)
3. [Problem Statement](#3-problem-statement)
4. [Goals & Success Metrics](#4-goals--success-metrics)
5. [Core Features & Requirements](#5-core-features--requirements)
6. [Technical Architecture](#6-technical-architecture)
7. [Non-Functional Requirements](#7-non-functional-requirements)
8. [Risk Register](#8-risk-register)
9. [Release Plan](#9-release-plan)
10. [Open Questions & Decisions Needed](#10-open-questions--decisions-needed)
11. [Appendix](#11-appendix)

---

## 1. Document Overview

| Field | Details |
|---|---|
| Product Name | CrossMarket Price Analyzer |
| Version | 1.0 |
| Date | March 30, 2026 |
| Author | Product Team |
| Status | Draft |
| Target Release | Q3 2026 (MVP) |

---

## 2. Executive Summary

CrossMarket Price Analyzer is a web-based tool that identifies profitable cross-border trade opportunities between U.S. and Vietnam markets. The system continuously scrapes product prices from U.S. retail/wholesale sources and Vietnamese e-commerce platforms, normalizes data across currencies and packaging formats, calculates fully-loaded landed costs (including shipping, import duties, and handling), and ranks products by profit margin potential.

The tool targets small-to-medium importers, resellers, and entrepreneurs who want data-driven decisions on which U.S. products to bring into the Vietnamese market. By automating price discovery, cost estimation, and opportunity scoring, the tool eliminates hours of manual research and provides a continuously updated view of market arbitrage opportunities.

---

## 3. Problem Statement

### 3.1 Current Pain Points

- Manual price comparison across multiple U.S. and Vietnam marketplaces is time-consuming and error-prone.
- Landed cost calculations (shipping, duties, taxes) are complex and vary by product category.
- No centralized tool exists to combine price data, cost estimation, and demand signals into a single actionable view.
- Market conditions change rapidly; static spreadsheets become outdated within days.

### 3.2 Target Users

| User Persona | Description | Primary Need |
|---|---|---|
| Cross-border Reseller | Individual or small business importing U.S. goods to sell in Vietnam | Find high-margin products quickly |
| E-commerce Seller | Shopee/Lazada/Tiki seller sourcing from U.S. suppliers | Compare landed costs vs. local competition |
| Market Analyst | Business analyst evaluating import opportunities | Data-driven market gap reports |

---

## 4. Goals & Success Metrics

### 4.1 Business Goals

1. Reduce product research time from hours to minutes for cross-border opportunities.
2. Deliver accurate landed cost estimates within 5% of actual costs.
3. Surface actionable opportunities with ≥15% profit margin.
4. Build a scalable platform supporting 10,000+ product comparisons.

### 4.2 Key Performance Indicators (KPIs)

| KPI | Target (MVP) | Target (v2.0) |
|---|---|---|
| Products tracked | 5,000+ | 50,000+ |
| Price refresh frequency | Daily | Every 6 hours |
| Landed cost accuracy | ±10% | ±5% |
| User adoption (MAU) | 100 | 1,000 |
| Average session time | >5 min | >10 min |
| Export usage rate | >30% | >50% |

---

## 5. Core Features & Requirements

### 5.1 Data Collection Engine

Automated scraping and API integration layer that collects product data from U.S. and Vietnam sources.

#### 5.1.1 U.S. Data Sources

- Retail sites: Amazon.com, Walmart.com, niche suppliers (e.g., cigarpage.com)
- Wholesale sites: Alibaba (US warehouse), wholesale club sites
- Implementation: Scrapy/Playwright for dynamic-rendered sites; requests + BeautifulSoup for static pages

#### 5.1.2 Vietnam Data Sources

- Shopee.vn (via Shopee Open Platform API)
- Lazada.vn (via Lazada Open Platform API)
- Tiki.vn (scraping + unofficial API endpoints)
- Specialty/niche shops via targeted scraping

#### 5.1.3 Data Points Collected

| Field | Description | Required |
|---|---|---|
| product_name | Full product title | Yes |
| brand | Brand/manufacturer name | Yes |
| sku | SKU or model number | If available |
| price | Listed price in original currency | Yes |
| currency | USD or VND | Yes |
| quantity_per_unit | Items per package (e.g., 20 cigars/box) | Yes |
| unit_price | Calculated price per single unit | Yes |
| seller_name | Retailer or seller identity | Yes |
| seller_rating | Rating/review score | If available |
| sales_volume | Monthly or total sales count | If available |
| url | Product listing URL | Yes |
| scraped_at | Timestamp of data collection | Yes |

### 5.2 Data Normalization Layer

- **Currency conversion:** Real-time USD → VND exchange rate via a forex API (e.g., exchangerate-api.com, Open Exchange Rates).
- **Unit standardization:** Normalize packaging differences (e.g., box of 20 vs. single unit vs. carton of 10 boxes) to a common per-unit price.
- **String cleaning:** Normalize product names (remove special characters, standardize brand casing) for better matching.
- **Category mapping:** Map products to HS code categories for duty/tax lookup.

### 5.3 Price Comparison Engine

Core matching and calculation module that pairs U.S. products with Vietnam listings and computes profitability.

#### 5.3.1 Product Matching

- Fuzzy string matching (Levenshtein distance, TF-IDF cosine similarity) on product names.
- Brand + SKU exact match as primary key when available.
- Confidence score (0–100%) for each match; only show matches above configurable threshold (default: 70%).
- Manual override: Allow users to confirm or reject auto-matches.

#### 5.3.2 Landed Cost Calculation

**Formula:**

> **Landed Cost = US Price + Shipping + Import Duty + VAT + Handling Fees**

| Cost Component | Source / Method | Notes |
|---|---|---|
| U.S. Product Price | Scraped from source | Converted to VND at current rate |
| International Shipping | Courier API estimates (FedEx, DHL, UPS) | Based on weight/dimensions; fallback to weight-tier estimates |
| Import Duty | Vietnam Customs tariff tables by HS code | Rates vary 0–50% depending on product category |
| VAT | Standard Vietnam VAT rate | Currently 10% on CIF value |
| Handling / Misc. | Configurable flat fee or percentage | Customs brokerage, local delivery, packaging |

#### 5.3.3 Profitability Metrics

- **Price Difference** = Vietnam Retail Price – Landed Cost
- **Profit Margin (%)** = (Price Difference / Vietnam Retail Price) × 100
- **ROI (%)** = (Price Difference / Landed Cost) × 100

### 5.4 Evaluation & Ranking Engine

Multi-factor scoring system that ranks products by overall opportunity potential.

| Factor | Weight (Default) | Data Source |
|---|---|---|
| Profit Margin % | 40% | Calculated from price comparison |
| Market Demand | 25% | Sales volume, review count on Vietnam platforms |
| Competition Level | 20% | Number of active sellers for same product in Vietnam |
| Price Stability | 10% | Historical price variance over trailing 30 days |
| Match Confidence | 5% | Fuzzy matching confidence score |

- Composite score (0–100) determines ranking on the dashboard.
- Users can adjust factor weights via UI sliders to match their strategy.

### 5.5 User Interface (Dashboard)

#### 5.5.1 Dashboard Views

- **Opportunity Feed:** Ranked list of products sorted by composite score, with key metrics at a glance.
- **Side-by-Side Comparison:** Detailed view showing U.S. vs. Vietnam pricing, cost breakdown, and margin for a selected product.
- **Category Explorer:** Browse opportunities by product category with aggregate stats.
- **Price History Chart:** Line chart showing price trends over time for tracked products.

#### 5.5.2 Filters & Controls

- Category filter (dropdown with HS code categories)
- Minimum margin % slider
- Demand level filter (low / medium / high)
- Competition filter (low / medium / high)
- Date range picker for price history
- Source marketplace filter (Shopee, Lazada, Tiki)

#### 5.5.3 Export Capabilities

- CSV export of filtered product list with all metrics
- Excel export with formatted sheets (summary + detailed breakdown)
- PDF report generation for business plans / investor presentations

### 5.6 URL-Based Quick Lookup

Allows users to paste a U.S. product URL and get an instant opportunity analysis.

1. User pastes a product URL (e.g., from cigarpage.com or Amazon).
2. System scrapes product details and price from the URL.
3. System searches Vietnam marketplaces for matching products.
4. System calculates landed cost and compares with Vietnam retail prices.
5. System returns a summary card with margin %, demand level, and competition assessment.

---

## 6. Technical Architecture

### 6.1 Tech Stack

| Layer | Technology | Rationale |
|---|---|---|
| Backend / API | Python 3.11+ with FastAPI | Async support, auto-generated OpenAPI docs, high performance |
| Scraping | Scrapy + Playwright | Scrapy for scale/scheduling; Playwright for JS-rendered pages |
| Database | PostgreSQL 15+ | Relational integrity for product mappings; JSONB for flexible metadata |
| Cache | Redis | Rate limiting, session cache, real-time exchange rate cache |
| Task Queue | Celery + Redis | Async scraping jobs, scheduled price refreshes |
| Scheduler | Celery Beat (or Airflow for complex DAGs) | Cron-like scheduling for data collection pipelines |
| Frontend | React 18 + TypeScript | Component-based UI, rich interactivity for dashboards |
| Charts | Recharts or Chart.js | Lightweight, React-native charting for price trends |
| Deployment | Docker + Docker Compose | Reproducible environments, easy local dev and cloud deploy |
| CI/CD | GitHub Actions | Automated testing, linting, deployment pipeline |

### 6.2 System Architecture Diagram

The system follows a modular, event-driven architecture with the following high-level components:

- **Scraping Workers:** Headless browser pool (Playwright) and HTTP scrapers (Scrapy) that collect data on schedule.
- **Data Pipeline:** Normalization, deduplication, and enrichment before storage.
- **Matching Service:** Fuzzy matching engine that pairs U.S. and Vietnam products.
- **Cost Calculator:** Integrates shipping APIs, tariff tables, and forex rates to compute landed costs.
- **Scoring Engine:** Applies weighted multi-factor scoring to rank opportunities.
- **API Layer:** FastAPI REST endpoints serving the frontend dashboard.
- **Frontend App:** React SPA consuming the API and rendering interactive dashboards.

### 6.3 Database Schema (Key Tables)

| Table | Purpose | Key Columns |
|---|---|---|
| products | Master product records | id, name, brand, sku, category, hs_code |
| price_snapshots | Historical price records | id, product_id, price, currency, source, scraped_at |
| product_matches | US ↔ VN product pairings | id, us_product_id, vn_product_id, confidence_score, status |
| cost_estimates | Landed cost breakdowns | id, match_id, shipping, duty, vat, handling, total_landed |
| opportunity_scores | Ranked opportunity records | id, match_id, margin_pct, demand_score, competition_score, composite_score |
| exchange_rates | Cached forex rates | id, from_currency, to_currency, rate, fetched_at |

---

## 7. Non-Functional Requirements

| Requirement | Specification |
|---|---|
| Performance | Dashboard loads in <2s; API responses <500ms for cached queries |
| Scalability | Handle 50,000+ products; horizontal scaling via Docker containers |
| Availability | 99.5% uptime target; graceful degradation if scraping sources are down |
| Data Freshness | Prices refreshed at least daily; exchange rates every hour |
| Security | HTTPS everywhere; API key auth; input sanitization; no PII stored |
| Compliance | Respect robots.txt; rate-limit scraping to avoid IP bans; comply with platform ToS |
| Monitoring | Structured logging; Prometheus metrics; alerts for scraping failures |
| Backup | Daily PostgreSQL backups; 30-day retention |

---

## 8. Risk Register

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Website structure changes break scrapers | High | High | Modular scraper design; automated tests; alerting on parse failures |
| IP blocking by target sites | High | Medium | Rotating proxies; rate limiting; respect robots.txt |
| Inaccurate product matching | Medium | Medium | Confidence thresholds; manual review queue; user feedback loop |
| Exchange rate volatility | Medium | Low | Hourly rate refresh; show rate timestamp; allow manual override |
| Shipping cost estimation errors | Medium | Medium | Multiple courier API fallbacks; weight-tier lookup table as baseline |
| Platform API deprecation / ToS changes | High | Low | Abstract data sources behind interfaces; monitor API changelogs |
| Tariff/duty rate changes | Medium | Low | Monthly refresh of HS code tariff tables; admin override capability |

---

## 9. Release Plan

### 9.1 MVP (v1.0) — Target: Q3 2026

- Core scraping for 2–3 U.S. sources + Shopee.vn
- Basic fuzzy matching engine
- Landed cost calculator with manual shipping/duty inputs
- Dashboard with opportunity feed and side-by-side comparison
- CSV export
- URL-based quick lookup

### 9.2 v1.5 — Target: Q4 2026

- Add Lazada.vn and Tiki.vn data sources
- Integrate FedEx/DHL shipping APIs for automated estimates
- HS code auto-detection and duty rate lookup
- Price history charts
- Excel export with formatted reports

### 9.3 v2.0 — Target: Q1 2027

- Full Shopee/Lazada API integration for demand signals (sales volume, reviews)
- Multi-factor scoring engine with user-configurable weights
- Alerting system: email/Telegram notifications for new high-margin opportunities
- Scheduled automated reports
- User accounts and saved product watchlists
- PDF report generation

---

## 10. Open Questions & Decisions Needed

| # | Question | Owner | Status |
|---|---|---|---|
| 1 | Which specific U.S. product categories to prioritize for MVP? | Product / Business | Open |
| 2 | Shopee Open Platform API approval timeline and rate limits? | Engineering | Open |
| 3 | Preferred hosting provider (AWS / GCP / VPS)? | Engineering / Ops | Open |
| 4 | Legal review of scraping compliance for each target site? | Legal | Open |
| 5 | Budget allocation for rotating proxy service? | Business / Engineering | Open |
| 6 | Will users need authentication / multi-tenancy in MVP? | Product | Open |
| 7 | Preferred notification channels (email, Telegram, Zalo)? | Product / Users | Open |

---

## 11. Appendix

### 11.1 Glossary

| Term | Definition |
|---|---|
| HS Code | Harmonized System code used internationally to classify traded products for customs/tariff purposes |
| Landed Cost | Total cost of a product delivered to the destination, including purchase price, shipping, duties, taxes, and fees |
| CIF | Cost, Insurance, and Freight — the total cost including product, insurance, and shipping to the destination port |
| Fuzzy Matching | Approximate string matching techniques that find similar (not identical) text strings |
| MAU | Monthly Active Users |
| VND | Vietnamese Dong (official currency of Vietnam) |

### 11.2 Reference Links

- Shopee Open Platform: https://open.shopee.com
- Lazada Open Platform: https://open.lazada.com
- Vietnam Customs Tariff Database: https://tongcuchaiquan.gov.vn
- HS Code Lookup: https://hts.usitc.gov
- Open Exchange Rates API: https://openexchangerates.org
