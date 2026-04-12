# UAT Test Plan — MVP v1.0

**CrossMarket Price Analyzer**
**Document Status:** Draft
**Version:** 1.0

| Field | Details |
|---|---|
| **Project** | CrossMarket Price Analyzer |
| **Release** | MVP v1.0 |
| **Test Phase** | User Acceptance Testing |
| **Date** | TBD — to be scheduled after all Phase 2 tasks are complete |
| **Tester** | Internal team |
| **Environment** | Staging — `docker compose --profile observability` |
| **Entry Criteria** | All Phase 2 tasks complete; CI pipeline passing; all unit and integration tests green |
| **Exit Criteria** | All P0 and P1 test scenarios pass; P2 scenarios pass or documented as known limitations; no critical or high severity bugs open |

---

## Table of Contents

1. [Test Scope](#1-test-scope)
2. [Test Environment Setup](#2-test-environment-setup)
3. [Test Scenarios](#3-test-scenarios)
4. [Defect Severity Definitions](#4-defect-severity-definitions)
5. [Risk Assessment](#5-risk-assessment)
6. [Sign-Off](#6-sign-off)

---

## 1. Test Scope

### In Scope

- Happy-path end-to-end flows for all MVP v1.0 features
- User interactions on the React SPA (Dashboard, Comparison, Quick Lookup, Price History, Alerts, Settings)
- Backend API correctness via frontend interactions
- Resilience and error handling for known failure modes
- Internationalization (EN / VI)
- Infrastructure: Docker health checks, CD pipeline, observability stack
- Outbox pattern for reliable event publishing

### Out of Scope

- Performance/load testing (deferred to Phase 3, k6 load tests)
- Security penetration testing (deferred to Phase 4)
- Shopee / Lazada / Tiki API integration (v1.5 scope)
- User authentication and multi-tenancy (v2.0 scope)
- FedEx / DHL shipping API integration (v1.5 scope)
- PDF export (v2.0 scope)

### Test Data Requirements

- At least **50 scraped U.S. products** and **50 matched Vietnam products** must exist in the staging database before UAT begins.
- Products should span **at least 3 categories** (e.g., Cosmetics, Electronics, Food & Beverage).
- At least **10 products** should have a margin ≥ 15% to support positive-scenario testing.
- Exchange rate in staging should reflect a realistic rate (≈ 25,000 VND/USD).

---

## 2. Test Environment Setup

### Prerequisites

```bash
# 1. Ensure you are on the staging branch or a release candidate branch
git checkout main
git pull origin main

# 2. Start infrastructure with observability stack
docker compose --profile observability up -d

# 3. Verify all containers are healthy
docker compose ps

# 4. Run database migrations
dotnet ef database update \
  --project src/Common/Common.Infrastructure \
  --startup-project src/Services/ProductService/ProductService.Api

# 5. Seed test data (if a seed script exists)
dotnet run --project scripts/DataSeeder/DataSeeder.csproj

# 6. Build the React SPA
cd src/Apps/CMA.WebApp && npm install && npm run build && cd ../..

# 7. Verify the gateway is responding
curl -s http://localhost:8080/api/opportunities | jq '.total'
```

### Expected Service URLs

| Service | URL | Health Check |
|---|---|---|
| API Gateway | `http://localhost:8080` | HTTP 200 |
| ProductService | `http://localhost:5001` | `GET /health` |
| MatchingService | `http://localhost:5002` | `GET /health` |
| ScoringService | `http://localhost:5003` | `GET /health` |
| NotificationService | `http://localhost:5004` | `GET /health` |
| Prometheus | `http://localhost:9090` | HTTP 200 |
| Grafana | `http://localhost:3000` | HTTP 200 |
| RabbitMQ Management | `http://localhost:15672` | HTTP 200 |
| MySQL | `localhost:3306` | TCP check |
| Redis | `localhost:6379` | TCP check |

---

## 3. Test Scenarios

---

### TS-01: End-to-End Opportunity Flow

**Feature:** Dashboard → Product Comparison → Confirm Match

**Objective:** Verify the full happy-path flow from browsing opportunities to confirming a match.

**Pre-conditions:**
- At least 5 products exist in the staging database with confirmed US→VN matches.
- At least 3 products have Composite Score ≥ 60.

**Steps:**

1. Open the Dashboard (`http://localhost:8080`).
2. Verify the opportunity feed loads with at least 5 cards.
3. Verify each card shows: Product Name, US→VN prices, Composite Score badge, Margin %.
4. Verify cards are sorted by Composite Score descending.
5. Click on any opportunity card.
6. Verify the Comparison page loads (`/compare/:matchId`).
7. Verify the US Product panel shows: Source, Price (USD), Seller, Seller Rating.
8. Verify the Vietnam Product panel shows: Source, Price (VND), Seller, Seller Rating.
9. Click the **Cost Breakdown** tab.
10. Verify the table shows all cost components: US Purchase Price, Shipping, Import Duty, VAT, Handling, Total Landed Cost.
11. Verify Total Landed Cost = sum of all above components.
12. Verify both USD and VND columns are populated.
13. Click the **Score Breakdown** tab.
14. Verify the 5-factor table is displayed with raw scores, weights, and weighted scores.
15. Click **Confirm Match** (green button).
16. Verify a success toast notification appears.
17. Verify the match status changes (e.g., "Confirmed" badge appears).
18. Navigate back to the Dashboard.
19. Apply a **Status = Pending** filter.
20. Verify the confirmed product is no longer visible in the Pending list.

**Expected Results:**
- Dashboard loads within 3 seconds (cached data).
- All panels on the Comparison page display correct, non-empty data.
- Confirm Match updates the match status in the database.
- Dashboard filter correctly excludes confirmed matches.

**Status:** [Pending]

**Severity:** P0

---

### TS-02: Quick Lookup Flow

**Feature:** Quick Lookup — URL Analysis

**Objective:** Verify that entering a valid U.S. product URL triggers scraping, matching, and scoring.

**Pre-conditions:**
- Scraping workers must be running (`dotnet run --project src/Services/ScrapingService/ScrapingService.Worker`).
- At least one valid, publicly accessible Amazon or Walmart product URL.

**Steps:**

1. Navigate to the **Quick Lookup** page via the sidebar.
2. Verify the URL input field and **Analyze** button are visible.
3. Enter a valid Amazon product URL (e.g., `https://www.amazon.com/dp/B0EXAMPLE`).
4. Click **Analyze**.
5. Verify a loading indicator appears.
6. Wait up to 60 seconds for the result.
7. Verify the **Product Details Card** is displayed with: Name, Brand, Price (USD), Source URL.
8. Verify at least one **Vietnam Match** card is shown (if matching products exist in the database).
9. Verify each Vietnam Match shows: VN Price (VND), Source, estimated Margin %.
10. Verify the **Estimated Landed Cost** is displayed.
11. If no Vietnam matches are found, verify the appropriate "No matches found" message is displayed.

**Expected Results:**
- Valid URL → results displayed within 60 seconds.
- Product details card is fully populated.
- Vietnam matches (if found) display VND prices and margin estimates.
- Invalid or inaccessible URL → graceful error message (not a crash).

**Status:** [Pending]

**Severity:** P0

---

### TS-03: Price History

**Feature:** Price History Chart and Data

**Objective:** Verify that historical price data renders correctly with filtering and export.

**Pre-conditions:**
- At least 3 products must have ≥ 10 price snapshots spanning at least 7 days.

**Steps:**

1. Navigate to the Dashboard.
2. Click on any opportunity card to open the Comparison page.
3. Click the **History** tab (or navigate to `/history/:productId`).
4. Verify the line chart renders with two lines: US Price (blue) and VN Price (orange).
5. Verify the chart has labeled axes and a legend.
6. Verify the default date range is **30 Days**.
7. Click **7 Days** preset. Verify the chart updates.
8. Click **90 Days** preset. Verify the chart updates.
9. Click **Custom**. Select a date range of at least 14 days. Verify the chart updates.
10. Scroll to the **Data Table** below the chart.
11. Verify the table shows: Date, US Price (USD), VN Price (VND), Exchange Rate, Source.
12. Verify the table rows are sorted by Date descending.
13. Click the **Export** button above the chart.
14. Select **CSV**.
15. Verify the browser downloads a `.csv` file.
16. Open the CSV and verify it contains at least the columns: Date, US Price, VN Price, and that data matches the table.

**Expected Results:**
- Chart renders without errors for products with sufficient data.
- All three date range presets function correctly.
- Data table is accurate and sortable.
- CSV export contains correct data and correct columns.

**Status:** [Pending]

**Severity:** P1

---

### TS-04: CSV Export

**Feature:** Dashboard CSV Export

**Objective:** Verify CSV export produces a well-formed file with all filtered results.

**Pre-conditions:**
- At least 20 opportunities must exist in the database.

**Steps:**

1. Open the Dashboard with no filters applied.
2. Click **Load More** until at least 20 cards are visible.
3. Click the **Export** button.
4. Select **CSV**.
5. Verify the browser downloads `opportunities-export.csv`.
6. Open the CSV file in a spreadsheet application (Excel, Google Sheets, or equivalent).
7. Verify the file has a **header row**.
8. Verify the header columns include: Product Name, US Price (USD), VN Price (VND), Composite Score, Margin %, Demand Score, Competition Score, US Source, VN Source, Calculated At.
9. Verify every row has data in all columns (no completely empty rows).
10. Apply a **Category** filter (select any category with results).
11. Click Export again.
12. Verify the filtered export contains only products matching the selected category.
13. Verify the row count in the filtered export ≤ the unfiltered export row count.

**Expected Results:**
- CSV is valid UTF-8, opens without encoding errors.
- All required columns are present.
- Filter state is respected in the export.
- Rows match the currently loaded (not just filtered) set.

**Status:** [Pending]

**Severity:** P1

---

### TS-05: Filter Persistence

**Feature:** Dashboard Filter State

**Objective:** Verify filter behavior — filters do NOT persist across page navigation in v1.0.

**Pre-conditions:**
- At least 10 opportunities in at least 2 different categories.

**Steps:**

1. Open the Dashboard with no filters.
2. Apply a **Category** filter to any specific category (e.g., Cosmetics).
3. Apply a **Minimum Margin** filter of 10%.
4. Verify the feed updates to show only filtered results.
5. Navigate to the **Settings** page via the sidebar.
6. Verify the page loads.
7. Navigate back to the **Dashboard**.
8. Verify the feed has **reset to show all opportunities** (no filters applied).
9. Apply a **Source = Amazon** filter.
10. Navigate directly to the Comparison page by clicking on a card.
11. Press the browser **Back** button.
12. Verify the Source = Amazon filter is **not** restored.

**Expected Results:**
- Filters reset on navigation away from the Dashboard (intentional v1.0 behavior — no filter persistence).
- Feed always loads with sensible defaults after navigation.

**Status:** [Pending]

**Severity:** P2

**Note:** This is a **known limitation** in v1.0. Filter persistence via URL query params is planned for a future release.

---

### TS-06: Resilience — Scraping Failure

**Feature:** Graceful Error Handling for Invalid URLs

**Objective:** Verify Quick Lookup handles invalid or inaccessible URLs without crashing.

**Pre-conditions:**
- ScrapingService must be running.

**Steps:**

1. Navigate to the **Quick Lookup** page.
2. Enter each of the following URLs and click **Analyze**:
   a. `https://www.amazon.com/product-that-does-not-exist-12345`
   b. `https://notavalidurl.com/page`
   c. An empty string
   d. A URL with only spaces
3. For each test, verify the UI does **not** crash (no blank white page, no browser error).
4. Verify an error message is displayed: appropriate, human-readable, and in the correct language.
5. Verify the page remains usable (can navigate away, can enter a new URL).

**Expected Results:**
- No crashes or unhandled exceptions.
- Error messages are clear and actionable.
- Error messages appear in the user's selected language (EN or VI).

**Status:** [Pending]

**Severity:** P0

---

### TS-07: Resilience — Exchange Rate API Down

**Feature:** Fallback Exchange Rate

**Objective:** Verify the system uses a fallback exchange rate when the live forex API is unavailable.

**Pre-conditions:**
- Must be able to simulate the exchange rate API being down (either via network isolation or feature flag).

**Steps:**

1. Confirm the application is running normally and the current exchange rate is loaded.
2. **Simulate API failure** — block outbound traffic to the exchange rate API domain (e.g., via `docker compose exec scoring curl -X PUT ...` to set a feature flag, or block port 443 outbound on the container).
3. Trigger a score recalculation via `PUT http://localhost:5003/api/scores/recalculate` (or trigger via the Settings UI if available).
4. Check the **ScoringService logs**:
   ```
   docker compose logs scoring-service | grep "exchange"
   ```
5. Verify a **WARN** or **INFO** log entry appears indicating the fallback rate of **25,000 VND/USD** is being used.
6. Open the Dashboard or Comparison page.
7. Verify prices in VND are still displayed (using the fallback rate).
8. Verify the exchange rate shown in the Cost Breakdown tab reflects the fallback rate.
9. **Restore** the exchange rate API connectivity.
10. Verify the system returns to using the live rate (check the timestamp in the Cost Breakdown tab).

**Expected Results:**
- System does not fail when exchange rate API is unavailable.
- Fallback rate (25,000 VND/USD) is used silently.
- Warning is logged for observability.
- System recovers automatically when the API is restored.

**Status:** [Pending]

**Severity:** P1

---

### TS-08: Scoring Weight Adjustment

**Feature:** Settings — Scoring Weights

**Objective:** Verify scoring weights can be adjusted and are persisted correctly.

**Pre-conditions:**
- ScoringService must be running.
- At least 10 scored opportunities must exist.

**Steps:**

1. Navigate to the **Settings** page.
2. Scroll to the **Scoring Weights** section.
3. Verify 5 sliders are displayed: Profit Margin, Market Demand, Competition, Price Stability, Match Confidence.
4. Verify the default weights are: 40%, 25%, 20%, 10%, 5% (sum = 100%).
5. Move the **Profit Margin** slider to **80**.
6. Observe that the other sliders auto-adjust downward to keep the total at 100%.
7. Click **Save**.
8. Verify a **success toast** notification appears.
9. Navigate to the **Dashboard**.
10. Verify the opportunity order has changed (reflecting the new weights).
11. Navigate back to **Settings**.
12. Verify the **Profit Margin** slider is still at 80.
13. Click **Reset to Defaults**.
14. Verify all sliders return to their original values (40/25/20/10/5).
15. Click **Save**.

**Expected Results:**
- Sliders are adjustable and enforce 100% total.
- Weights are persisted to the server (visible after page refresh).
- Dashboard ranking changes after weight adjustment.
- Reset restores defaults correctly.

**Status:** [Pending]

**Severity:** P0

---

### TS-09: i18n — Vietnamese Locale

**Feature:** Internationalization (EN / VI)

**Objective:** Verify all UI strings update correctly when switching between English and Vietnamese.

**Pre-conditions:**
- React SPA must be built and serving the `i18n/` translation files.

**Steps:**

1. Open the Dashboard in **English** (default).
2. Verify the following static strings are in English:
   - Sidebar: Dashboard, Compare, Categories, History, Quick Lookup, Alerts, Settings.
   - Filter bar labels: Category, Source, Minimum Margin, etc.
   - Buttons: Export, Load More, Confirm Match, Reject Match, etc.
3. Click the **user menu / language selector** in the header.
4. Switch to **Tiếng Việt** (Vietnamese).
5. Verify the sidebar labels update to Vietnamese.
6. Verify the filter bar labels update to Vietnamese.
7. Verify all buttons and badges update to Vietnamese.
8. Navigate to the **Comparison** page. Verify all labels are in Vietnamese.
9. Navigate to the **Settings** page. Verify all labels and slider names are in Vietnamese.
10. Navigate to the **Quick Lookup** page. Verify the placeholder text is in Vietnamese.
11. Navigate to the **Alerts** page. Verify all strings are in Vietnamese.
12. Switch back to **English**. Verify all strings revert correctly.
13. Refresh the page while in Vietnamese mode. Verify the language persists.

**Expected Results:**
- All static UI strings are translated in Vietnamese.
- Navigation between pages does not reset the language choice.
- Page refresh respects the selected language.
- No untranslated strings (no "Translation missing" fallbacks or raw key names).

**Status:** [Pending]

**Severity:** P1

---

### TS-10: Docker Health Checks

**Feature:** Infrastructure — Container Health

**Objective:** Verify all Docker containers report healthy status.

**Pre-conditions:**
- `docker compose --profile observability up -d` must have been run successfully.

**Steps:**

1. Open a terminal on the staging server.
2. Run `docker compose ps`.
3. Verify the **STATUS** column shows `healthy` for all services:
   - `product-service`
   - `matching-service`
   - `scoring-service`
   - `notification-service`
   - `scraping-worker`
   - `api-gateway`
   - `mysql`
   - `redis`
   - `rabbitmq`
4. For any container that does **not** show `healthy`, run:
   ```
   docker compose logs <service-name> --tail 50
   ```
   and record the error output.
5. Run `docker compose exec rabbitmq rabbitmq-diagnostics -q ping` to verify RabbitMQ is responding.
6. Run `docker compose exec redis redis-cli ping` to verify Redis is responding.
7. Run `mysql -h localhost -u cma_user -p -e "SELECT COUNT(*) FROM cma_products.products;"` to verify the database is accessible and seeded.

**Expected Results:**
- All containers report `healthy` status.
- RabbitMQ, Redis, and MySQL all respond to connectivity checks.
- At least 50 products are seeded in the database.

**Status:** [Pending]

**Severity:** P0

---

### TS-11: CD Pipeline — Staging Deploy

**Feature:** GitHub Actions CI/CD

**Objective:** Verify the CD pipeline builds Docker images and deploys to staging on merge to main.

**Pre-conditions:**
- CI/CD workflow must be configured in `.github/workflows/`.
- Access to the staging server or deployment environment.

**Steps:**

1. On a feature branch, make a trivial change (e.g., update a comment in any source file).
2. Push the branch and open a Pull Request.
3. Verify the CI pipeline runs automatically:
   - Build step passes (`dotnet build`)
   - Unit tests pass (`dotnet test`)
4. Merge the PR to `main`.
5. Navigate to the **GitHub Actions** tab for the repository.
6. Verify the **Deploy to Staging** workflow is triggered automatically.
7. Monitor the workflow run:
   - Verify Docker images are **built** for all 6 services.
   - Verify Docker images are **pushed** to the registry.
   - Verify staging deployment **steps** complete (apply migrations, restart services).
8. Once the workflow completes, verify the staging environment reflects the change (the trivial comment update will not be visible in the UI — use `docker compose logs` to confirm the new image was pulled).

**Expected Results:**
- CI pipeline triggers on PR and merge.
- All build and test steps pass.
- CD pipeline builds and pushes Docker images.
- Staging environment is updated automatically after merge to main.

**Status:** [Pending]

**Severity:** P0

---

### TS-12: Monitoring — Prometheus Metrics

**Feature:** Observability — Prometheus + Grafana

**Objective:** Verify service metrics are scraped and queryable in Prometheus.

**Pre-conditions:**
- `docker compose --profile observability` must be running.
- Prometheus must be configured to scrape all service endpoints.

**Steps:**

1. Open Prometheus web UI: `http://localhost:9090`.
2. Verify Prometheus is healthy (status page shows "Prometheus is healthy.").
3. Navigate to **Graph** → **Execute**.
4. Run the following queries and verify each returns data:

   | Query | Expected Metric |
   |---|---|
   | `http_requests_total` | Total HTTP requests across all services |
   | `http_request_duration_seconds_bucket` | Request latency histogram |
   | `db_query_duration_seconds` | Database query latency |
   | `cache_hits_total` | Redis cache hit count |
   | `cache_misses_total` | Redis cache miss count |
   | `scraping_jobs_total{status="completed"}` | Completed scraping job count |
   | `scraping_jobs_total{status="failed"}` | Failed scraping job count |
   | `opportunity_score_value` | Composite scores for opportunities |

5. Open Grafana: `http://localhost:3000` (default credentials: admin / admin).
6. Verify the **CrossMarket Price Analyzer** dashboard exists.
7. Verify panels show non-zero data (at least request rate, error rate, and job status).
8. Run a Quick Lookup (TS-02) to generate new HTTP traffic.
9. Refresh the Grafana dashboard. Verify the request count panel updated.

**Expected Results:**
- Prometheus successfully scrapes all 6 services.
- Metrics queries return non-empty results.
- Grafana dashboard loads with live data.

**Status:** [Pending]

**Severity:** P1

---

### TS-13: Outbox Pattern — RabbitMQ Failure

**Feature:** Reliable Messaging — Outbox Pattern

**Objective:** Verify events are processed from the outbox after RabbitMQ recovers from a failure.

**Pre-conditions:**
- RabbitMQ must be running.
- Outbox table must exist in the database (verify via: `SHOW TABLES LIKE '%outbox%'`).

**Steps:**

1. Start the application with all services running normally.
2. Trigger an event that publishes a message via the outbox — for example, confirm a match (TS-01, Step 15).
3. Verify the event is **not yet processed** by the downstream service (check the NotificationService logs).
4. **Stop RabbitMQ**:
   ```
   docker compose stop rabbitmq
   ```
5. While RabbitMQ is down, confirm another match (or trigger any event that publishes via the outbox).
6. Verify the event is **written to the outbox table** (check the database directly).
7. **Restart RabbitMQ**:
   ```
   docker compose start rabbitmq
   ```
8. Wait up to 30 seconds for the outbox processor to reconnect.
9. Check the downstream service logs. Verify the event from Step 5 is **processed** after RabbitMQ restart.
10. Check the outbox table. Verify the processed record is marked as sent or removed.
11. Verify the NotificationService received the downstream event.

**Expected Results:**
- Events are written to the outbox table even when RabbitMQ is unavailable.
- After RabbitMQ recovers, the outbox processor processes all pending events in order.
- No events are lost during the RabbitMQ downtime.

**Status:** [Pending]

**Severity:** P1

---

### TS-14: Manual Cost Override

**Feature:** Manual Override of Shipping and Duty Costs

**Objective:** Verify that manually overridden cost values are used in scoring instead of the auto-estimated values.

**Pre-conditions:**
- ScoringService must be running.
- At least 1 scored opportunity must exist with a valid `matchId`.

**Steps:**

1. Identify a match ID from the Dashboard (e.g., from the URL `/compare/<matchId>`).
2. Record the current total landed cost shown in the Cost Breakdown tab.
3. Run the following command to apply a manual override:

   ```bash
   curl -X PUT http://localhost:5003/api/scores/manual-costs \
     -H "Content-Type: application/json" \
     -d '{
       "matchId": "<your-match-id>",
       "shippingCostOverride": 25.00,
       "dutyCostOverride": 5.00,
       "handlingCostOverride": 2.00
     }'
   ```

4. Verify the API returns a `200 OK` response.
5. Refresh the Comparison page for that match.
6. Verify the **Cost Breakdown** tab now shows the overridden values instead of the auto-estimated ones.
7. Verify the **Total Landed Cost** reflects the override.
8. Verify the **Composite Score** has been recalculated based on the new landed cost.
9. Verify the **Margin %** and **Price Difference** have changed accordingly.
10. Click **Reset Override** (if available) or send a `DELETE` request to remove the override.
11. Refresh the page and verify the original auto-estimated values are restored.

**Expected Results:**
- Override values are persisted and used in scoring calculations.
- Composite Score updates after an override is applied.
- Override can be cleared and original values restored.
- Score recalculation is reflected on the Dashboard (opportunity ranking may change).

**Status:** [Pending]

**Severity:** P2

---

## 4. Defect Severity Definitions

| Severity | Definition | Examples |
|---|---|---|
| **P0 — Critical** | Feature is completely broken or data loss risk. No workaround available. All testing must halt until resolved. | Dashboard crashes on load; data corruption; scoring engine returns NaN; all exports fail. |
| **P1 — High** | Core feature is broken or severely degraded. A workaround may exist but the user experience is significantly impacted. | Quick Lookup returns 500 error for all URLs; CSV export produces empty files; scoring weights cannot be saved. |
| **P2 — Medium** | Feature works but with a significant bug or unexpected behavior. A reasonable workaround exists. Testing may continue. | Price chart does not render for products with <5 data points; CSV export ignores current page of pagination. |
| **P3 — Low** | Minor bug, cosmetic issue, or edge case. Does not block release. | Misspelled label in Vietnamese translation; tooltip appears in wrong position; minor layout shift on resize. |

---

## 5. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scraping services blocked by Amazon/Walmart during testing | Medium | Low | Use cigarpage.com for Quick Lookup tests; use pre-seeded data for Dashboard tests. |
| Exchange rate API rate-limited or unavailable | Low | Medium | Fallback rate is implemented (TS-07 validates this). |
| RabbitMQ crashes during outbox test (TS-13) | Low | Low | Test is designed to validate recovery from this exact scenario. |
| Test data not seeded before UAT starts | Medium | High | Enforce seed data check as a pre-UAT gate in the test environment setup steps. |
| CI/CD pipeline failures due to flaky tests | Low | Medium | All tests must pass in CI before merge; flaky tests to be logged and addressed. |
| Grafana default admin password not changed | Low | Medium | Change admin password before UAT begins; document in environment setup. |

---

## 6. Sign-Off

### Test Summary

| Scenario | ID | Severity | Result | Tester | Date |
|---|---|---|---|---|---|
| End-to-End Opportunity Flow | TS-01 | P0 | [Pending] | | |
| Quick Lookup Flow | TS-02 | P0 | [Pending] | | |
| Price History | TS-03 | P1 | [Pending] | | |
| CSV Export | TS-04 | P1 | [Pending] | | |
| Filter Persistence | TS-05 | P2 | [Pending] | | |
| Resilience — Scraping Failure | TS-06 | P0 | [Pending] | | |
| Resilience — Exchange Rate API Down | TS-07 | P1 | [Pending] | | |
| Scoring Weight Adjustment | TS-08 | P0 | [Pending] | | |
| i18n — Vietnamese Locale | TS-09 | P1 | [Pending] | | |
| Docker Health Checks | TS-10 | P0 | [Pending] | | |
| CD Pipeline — Staging Deploy | TS-11 | P0 | [Pending] | | |
| Monitoring — Prometheus Metrics | TS-12 | P1 | [Pending] | | |
| Outbox Pattern — RabbitMQ Failure | TS-13 | P1 | [Pending] | | |
| Manual Cost Override | TS-14 | P2 | [Pending] | | |

### Defects Found

| # | Description | Severity | Status | Assigned To |
|---|---|---|---|---|
| | | | | |

### Known Limitations

| Limitation | Description | Accepted By |
|---|---|---|
| Filter Persistence (TS-05) | Filters reset on navigation in v1.0. URL-based filter persistence is deferred. | |
| Manual Cost Override Reset | Reset functionality for manual overrides is P2 in v1.0; may not be available in initial UAT. | |

### UAT Sign-Off

| Role | Name | Signature | Date |
|---|---|---|---|
| Tester | | | |
| Product Owner | | | |
| QA Lead | | | |
| Tech Lead | | | |

**Final Result:** ✅ Pass — Ready for Release / ❌ Fail — Blocker(s) Must Be Resolved

---

*End of UAT Test Plan — CrossMarket Price Analyzer MVP v1.0*
