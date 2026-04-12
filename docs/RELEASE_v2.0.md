# CrossMarket Analyzer — v2.0 Release Documentation

> **Version**: 2.0.0 | **Release Date**: 2026-04-12 | **Status**: ✅ Complete

---

## Overview

v2.0 Full Platform adds identity & authentication, user watchlists, multi-channel alert notifications, and scheduled PDF/CSV reporting to the v1.5 foundation.

---

## New Services

| Service | Port | Database | Purpose |
|---|---|---|---|
| `AuthService.Api` | 5005 | `cma_auth` | JWT authentication, registration, login, watchlist, alert thresholds |

---

## Authentication (P4-B01, P4-B02)

### Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | No | Create account → returns JWT + refresh token |
| POST | `/api/auth/login` | No | Login → returns JWT + refresh token |
| POST | `/api/auth/refresh` | No | Rotate refresh token → returns new JWT + refresh |
| GET | `/api/watchlist` | Bearer | Paginated watchlist (page, pageSize) |
| POST | `/api/watchlist` | Bearer | Add product match to watchlist |
| DELETE | `/api/watchlist/{itemId}` | Bearer | Remove watchlist item |
| GET | `/api/alerts/thresholds` | Bearer | List alert thresholds |
| POST | `/api/alerts/thresholds` | Bearer | Create alert threshold |
| PUT | `/api/alerts/thresholds/{id}` | Bearer | Update threshold |
| DELETE | `/api/alerts/thresholds/{id}` | Bearer | Deactivate threshold |

### JWT Configuration

```json
// appsettings.json (AuthService.Api)
{
  "Jwt": {
    "Issuer": "CrossMarketAuth",
    "Audience": "CrossMarketApp",
    "SecretKey": "<min-32-chars-env-var>",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  }
}
```

### Token Lifetimes

- **Access token**: 60 minutes — contains `uid`, `email`, `role` claims
- **Refresh token**: 30 days, rotated on use — stored hashed in DB

---

## User Watchlist (P4-B03)

- Saved product matches with optional score alerts (above/below thresholds)
- Per-user isolation via `UserId` tenant key
- `IsMuted` flag to suppress notifications without removing the item

---

## Notification Service Extensions (P4-B04–B09)

### REST Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/notifications/email/preview` | Bearer | Preview HTML email for a match |
| POST | `/api/notifications/reports/opportunity` | Bearer | Generate PDF/CSV opportunity report |
| POST | `/api/notifications/telegram/preview` | Bearer | Preview Telegram markdown for a match |

### Background Services

- **`AlertThresholdEngine`**: Runs every 60 s; evaluates all active thresholds against current scores; dispatches Email/Telegram/InApp notifications
- **`ScheduledReportWorker`**: Runs at midnight + midday UTC; processes `ScheduledReports` table (daily/weekly/monthly)

### Configuration (NotificationService)

```json
// appsettings.json
{
  "SendGrid": { "ApiKey": "<env-var>" },
  "Telegram": { "BotToken": "<env-var>" },
  "App": { "BaseUrl": "https://app.crossmarket.example.com" }
}
```

---

## Frontend Changes (P4-F01–F08)

### New Pages

| Route | Page | Auth Required |
|-------|------|--------------|
| `/login` | `LoginPage.jsx` | No |
| `/register` | `RegisterPage.jsx` | No |
| `/watchlist` | `WatchlistPage.jsx` | Yes |
| `/profile` | `ProfilePage.jsx` | Yes |

### Updated Components

- **`Header.jsx`**: Shows user initials + logout when authenticated; profile link
- **`Sidebar.jsx`**: Added "Watchlist" nav item
- **`OpportunityCard.jsx`**: Added "Add to watchlist" button (bottom-right)
- **`ComparisonPage.jsx`**: Added "Export PDF" button + watchlist toggle
- **`App.jsx`**: All routes wrapped in `ProtectedRoute` (except `/login` and `/register`)

### Store Changes

- **`authStore.js`**: New Zustand store with `persist` middleware — manages access/refresh tokens + user identity
- **`axiosClient.js`**: Updated with silent JWT refresh on 401 + queue for concurrent refresh attempts

### Auth Flow

```
Register/Login → JWT stored in authStore → persisted to localStorage
API requests → axios interceptor reads token from authStore → attaches Bearer header
401 response → silent refresh attempt → new token → retry original request
```

---

## Running the Platform

```bash
# Start infrastructure
docker compose up -d mysql redis rabbitmq

# Start all backend services (from solution root)
dotnet run --project src/Services/AuthService/AuthService.Api
dotnet run --project src/Services/NotificationService/NotificationService.Api
dotnet run --project src/Services/ProductService/ProductService.Api
dotnet run --project src/Services/MatchingService/MatchingService.Api
dotnet run --project src/Services/ScoringService/ScoringService.Api
dotnet run --project src/Services/CMA.Gateway/CMA.Gateway.Api

# Start frontend
cd src/Apps/CMA.WebApp && npm run dev
```

---

## Database Schema

New tables created by `EnsureCreatedAsync()` in Development:

- `AuthService.cma_auth.users` — email, password_hash, full_name, refresh_token, timestamps
- `AuthService.cma_auth.watchlist_items` — user_id, match_id, product names, alert thresholds
- `AuthService.cma_auth.alert_thresholds` — user_id, channel, min_score, delivery_target
- `NotificationService.cma_notifications.alert_preferences` — per-user alert config
- `NotificationService.cma_notifications.delivery_logs` — sent notifications audit trail
- `NotificationService.cma_notifications.scheduled_reports` — report subscriptions
- `NotificationService.cma_notifications.user_telegram_configs` — Telegram chat IDs

---

## Breaking Changes

None — v2.0 is purely additive. Existing v1.5 APIs are unchanged.

---

## Known Limitations (v2.0)

| Item | Notes |
|------|-------|
| Real SendGrid integration | `EmailNotificationService` is mocked — real API in v2.1 |
| Real Telegram Bot | `TelegramNotificationService` is mocked — real bot in v2.1 |
| Rate limiting | No per-IP rate limit on auth endpoints — add in v2.1 |
| Account lockout | No brute-force lockout — add in v2.1 |
| Quartz.NET scheduler | `ScheduledReportWorker` uses `PeriodicTimer` — replace with Quartz.NET in v2.2 |
| Profile editing | User profile editing not yet exposed — stubbed in ProfilePage |

---

## Migration Notes

- `AuthService` database (`cma_auth`) is new — no migration from previous version needed
- JWT secret must be set via environment variable `Jwt:SecretKey` (min 32 chars)
- All users must re-register — no user data from v1.x is carried forward (by design)
