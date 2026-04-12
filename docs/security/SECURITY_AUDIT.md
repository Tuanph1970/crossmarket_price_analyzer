# P4-T01: Security Audit Report — v2.0

> **Scope**: CrossMarket Price Analyzer v2.0 (`CrossMarketAnalyzer.sln`)
> **Auditor**: Development team (self-assessment)
> **OWASP Top 10 (2021) coverage**

---

## A1 — Broken Access Control

| Item | Status | Details |
|------|--------|---------|
| JWT validation | ✅ PASS | All `/api/watchlist` and `/api/alerts/thresholds` endpoints use `.RequireAuthorization()`. `GetUserId()` extracts `uid` claim from JWT. |
| Multi-tenancy | ✅ PASS | `WatchlistItem` and `AlertThreshold` keyed by `UserId`; all handlers filter by `userId.Value`. No cross-tenant data leakage possible. |
| Role checks | ✅ PASS | JWT contains `role` claim; no admin endpoints exposed in AuthService. |
| CORS | ✅ PASS | Gateway (YARP) enforces CORS policy; backend services not directly internet-facing. |
| Path traversal | ✅ PASS | No file-system access in any API endpoint. |

## A2 — Cryptographic Failures

| Item | Status | Details |
|------|--------|---------|
| Password hashing | ✅ PASS | BCrypt with work factor 12 (`CrossMarket.SharedKernel/PasswordHasher`). |
| JWT secret | ⚠️ REVIEW | `JwtSettings.SecretKey` must be min 32 chars. Set via environment variable `Jwt:SecretKey` in production. Never commit to source. |
| HTTPS | ✅ PASS | All services behind YARP gateway with TLS termination. |
| Token storage | ✅ PASS | Refresh token stored hashed in DB; access token kept in memory/localStorage (not HttpOnly cookie — acceptable for SPA). |

## A3 — Injection

| Item | Status | Details |
|------|--------|---------|
| SQL injection | ✅ PASS | All DB access via Entity Framework Core (parameterized queries). No raw SQL. |
| Email/XSS injection | ✅ PASS | Email fields validated with regex in DTOs. HTML email templates escape user data. |
| Command injection | ✅ PASS | No `Process.Start` or shell execution. Scraping service uses Playwright selectors, not eval. |
| NoSQL injection | ✅ PASS | MongoDB not used. Redis access via typed SDK. |

## A4 — Insecure Design

| Item | Status | Details |
|------|--------|---------|
| Rate limiting | ⚠️ TODO v2 | No per-IP or per-user rate limit on `/api/auth/register` or `/api/auth/login`. Add `AspNetCoreRateLimit` middleware. |
| Account lockout | ⚠️ TODO v2 | Failed login attempts not tracked. Add brute-force lockout (e.g., 5 failures → 15 min lock). |
| Audit logging | ⚠️ TODO v2 | `DeliveryLog` tracks notification delivery; add `AuthAuditLog` for login/register events. |

## A5 — Security Misconfiguration

| Item | Status | Details |
|------|--------|---------|
| Default credentials | ✅ PASS | No default credentials. DB password via environment variable. |
| Debug mode | ✅ PASS | `EnsureCreatedAsync()` only runs in Development. |
| Stack trace exposure | ✅ PASS | `app.Environment.IsDevelopment()` gates detailed errors. |
| Unused HTTP methods | ✅ PASS | Only declared endpoints are registered. |

## A6 — Vulnerable & Outdated Components

| Item | Status | Details |
|------|--------|---------|
| NuGet packages | ✅ PASS | `dotnet list package --outdated` run; all packages up-to-date for .NET 9 GA. |
| Frontend deps | ✅ PASS | `npm audit` run; no critical/high severity vulnerabilities. |
| Playwright | ✅ PASS | Using latest chromium/firefox; auto-updated. |

## A7 — Authentication & Identity Failures

| Item | Status | Details |
|------|--------|---------|
| Password complexity | ✅ PASS | Zod schema requires min 6 chars. BCrypt work factor 12. |
| JWT expiration | ✅ PASS | Access token: 60 min. Refresh token: 30 days, rotated on use. |
| Token in URL | ⚠️ RISK | WebSocket uses `?access_token=` query string. Not logged by browsers, but appears in server logs. Use cookie-based approach in v2. |
| Sensitive data in token | ✅ PASS | JWT payload contains only `uid`, `email`, `role`. No PII. |

## A8 — Software & Data Integrity Failures

| Item | Status | Details |
|------|--------|---------|
| No software supply chain check | ⚠️ TODO v2 | Add `dotnet nuget verify` and `npm audit --audit-level=high` to CI. |
| Untrusted CI artifacts | ✅ PASS | Docker images built from pinned SHA tags, not `latest`. |

## A9 — Logging & Monitoring

| Item | Status | Details |
|------|--------|---------|
| Failed auth logging | ⚠️ TODO v2 | `UnauthorizedAccessException` logged at Warning level; add structured `AuthAttempt` event to ELK. |
| Health endpoints | ✅ PASS | `/health` on all services; k8s liveness/readiness probes configured. |
| Distributed tracing | ✅ PASS | OpenTelemetry + Serilog configured in `Common.Infrastructure`. |

## A10 — SSRF (Server-Side Request Forgery)

| Item | Status | Details |
|------|--------|---------|
| Scraping URLs | ⚠️ TODO v2 | `ScraperFactory` accepts any URL matching known domain patterns. Add URL allowlist. |
| Lazada/Tiki APIs | ✅ PASS | API keys stored in environment variables; not exposed to client. |

---

## Recommended Actions (Priority Order)

| Priority | Action | Owner |
|----------|--------|-------|
| HIGH | Set `Jwt:SecretKey` via env var in production (min 32 chars) | DevOps |
| HIGH | Add per-IP rate limiting on auth endpoints | Backend |
| HIGH | Add account lockout (5 failures → 15 min) | Backend |
| MEDIUM | Audit logging for auth events (login, register, logout) | Backend |
| MEDIUM | Add `dotnet nuget verify` + `npm audit --audit-level=high` to CI | DevOps |
| MEDIUM | SSRF allowlist for scraper URLs | Backend |
| LOW | Switch WebSocket auth to HttpOnly cookie | Backend |
| LOW | Structured AuthAuditLog to ELK | Backend |
