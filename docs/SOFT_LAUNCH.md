# P4-T07: Soft Launch Checklist — v2.0

## Definition

A **soft launch** (closed beta) opens the platform to a limited group of real users (up to 50) before public availability. The goal is to validate authentication, watchlists, and notifications work correctly with real data.

---

## Launch Criteria

- [x] All Phase 4 backend tasks implemented (P4-B01–B10)
- [x] All Phase 4 frontend tasks implemented (P4-F01–F08)
- [x] All unit tests passing (AuthService.Tests)
- [x] All Playwright E2E tests passing (auth.spec.js, features.spec.js)
- [x] Security audit completed (docs/security/SECURITY_AUDIT.md)
- [x] Performance benchmark scripts provided (tests/benchmark/README.md)
- [x] Production deployment guide complete (docs/DEPLOYMENT.md)
- [x] v2.0 release documentation complete (docs/RELEASE_v2.0.md)

---

## Pre-Launch Verification

### Authentication
- [ ] Register with a new email account → JWT + refresh token returned
- [ ] Login with valid credentials → redirected to dashboard
- [ ] Login with wrong password → "Invalid email or password" error (no account lock info)
- [ ] Refresh token after access token expires → seamless re-auth (no user prompt)
- [ ] Logout → session cleared, `/login` redirect
- [ ] Protected route without token → `/login` redirect with `state.from` preserved

### Watchlist
- [ ] Add product match to watchlist → appears in watchlist page
- [ ] Remove item from watchlist → disappears from list
- [ ] Watchlist persists after page reload (token persistence)
- [ ] Add to watchlist button visible on opportunity cards (authenticated users)
- [ ] Watchlist empty state shows "Browse opportunities" CTA

### Alerts
- [ ] Create email alert threshold → threshold listed in profile
- [ ] Delete alert threshold → removed from list
- [ ] Tab navigation between Profile / Thresholds / Reports works
- [ ] Profile shows correct user email and name

### Notifications
- [ ] PDF export button visible on comparison page
- [ ] PDF export triggers browser download (or shows "no data" if no match)
- [ ] `/api/notifications/email/preview` returns HTML (authenticated)
- [ ] `/api/notifications/telegram/preview` returns markdown (authenticated)
- [ ] Unauthenticated requests → 401

### Frontend UX
- [ ] `/login` not accessible when already authenticated
- [ ] `/register` not accessible when already authenticated
- [ ] Sidebar shows "Watchlist" link
- [ ] Header shows user initials (not "U" for logged-in user)
- [ ] Language switcher (EN/VI) works
- [ ] Mobile-responsive layout (sidebar collapses)

---

## Monitoring Setup

- [ ] Health endpoints monitored (uptimeRobot, Pingdom)
- [ ] Error rates tracked (New Relic / Sentry)
- [ ] AuthService logs shipped to ELK
- [ ] NotificationService delivery success rate tracked

---

## Beta User Communication

- [ ] Beta invite email sent (up to 50 users)
- [ ] Feature walkthrough documented (link to docs/RELEASE_v2.0.md)
- [ ] Feedback channel established (email / Discord / Linear)
- [ ] Known limitations communicated (see RELEASE_v2.0.md)

---

## Launch Sign-Off

| Role | Name | Date |
|------|------|------|
| Engineering Lead | | |
| Product Owner | | |
| QA | | |
| DevOps | | |

---

## Post-Launch (v2.1 Backlog)

Based on security audit and soft launch findings:

1. **Rate limiting on auth endpoints** (HIGH priority)
2. **Account lockout for brute-force protection** (HIGH priority)
3. **Real SendGrid integration** (MEDIUM)
4. **Real Telegram Bot integration** (MEDIUM)
5. **Profile editing UI** (LOW)
6. **Scheduled reports subscription UI** (LOW)
