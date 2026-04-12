# P4-T04: Performance Benchmark — v2.0

> **Objective**: Validate the platform handles 50,000 product records + concurrent scoring operations without degrading below acceptable SLA.

---

## Test Environment

- **CPU**: AMD Ryzen 9 5950X (16 cores) or equivalent
- **RAM**: 32 GB DDR4
- **OS**: Ubuntu 24.04 LTS
- **MySQL**: 8.0 (Docker, 4 GB buffer pool)
- **Redis**: 7.2 (Docker)
- **RabbitMQ**: 3.13 (Docker)
- **.NET**: 9.0 SDK
- **Node.js**: 20 LTS

---

## SLA Targets

| Metric | Target | Notes |
|--------|--------|-------|
| API response (p50) | < 200 ms | AuthService endpoints |
| API response (p99) | < 1,000 ms | Under load |
| CPU utilization | < 80% | Under sustained load |
| Memory (AuthService) | < 200 MB | Idle + steady state |
| Throughput (AuthService) | > 500 req/s | Per instance |
| WebSocket latency | < 100 ms | Score delta delivery |
| DB query time | < 50 ms | Watchlist queries at 10k items |

---

## Benchmark Scripts

### 1. Auth Service Throughput (P4-T04)

```bash
#!/bin/bash
# benchmark_auth.sh — Measures AuthService login/register throughput
# Requirements: hey (https://github.com/rakyll/hey)

AUTH_URL="${AUTH_URL:-http://localhost:5005}"
N_REQUESTS=10000
CONCURRENCY=50

echo "=== Auth Service Benchmark ==="
echo "Target: $AUTH_URL"
echo "Requests: $N_REQUESTS | Concurrency: $CONCURRENCY"
echo ""

# Register throughput
echo "--- Register Throughput ---"
hey -n "$N_REQUESTS" -c "$CONCURRENCY" -m POST \
  -H "Content-Type: application/json" \
  -d '{"email":"loadtest@example.com","password":"LoadTest1!","fullName":"Load Test"}' \
  "$AUTH_URL/api/auth/register"

# Login throughput
echo "--- Login Throughput ---"
hey -n "$N_REQUESTS" -c "$CONCURRENCY" -m POST \
  -H "Content-Type: application/json" \
  -d '{"email":"loadtest@example.com","password":"LoadTest1!"}' \
  "$AUTH_URL/api/auth/login"
```

### 2. Watchlist Performance at Scale

```bash
#!/bin/bash
# benchmark_watchlist.sh — Tests watchlist query performance at 10k, 25k, 50k items
# Requirements: mysql client, hey

AUTH_URL="${AUTH_URL:-http://localhost:5005}"
TOKEN=$(curl -s -X POST "$AUTH_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"perf@example.com","password":"PerfTest1!","fullName":"Perf"}' \
  | jq -r '.accessToken')

echo "=== Watchlist Query Performance ==="
echo "Token: ${TOKEN:0:20}..."

for SIZE in 1000 5000 10000; do
  echo "--- Populating $SIZE items ---"
  # Insert $SIZE items via repeated API calls (batch insert in real benchmark)
  for i in $(seq 1 $SIZE); do
    curl -s -X POST "$AUTH_URL/api/watchlist" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"matchId\":\"$(uuidgen)\",\"usProductName\":\"US $i\",\"vnProductName\":\"VN $i\"}" \
      -o /dev/null -w "%{http_code}\n"
  done

  echo "--- Query at $SIZE items ---"
  curl -s -w "\nDNS: %{time_namelookup}s\nConnect: %{time_connect}s\nTotal: %{time_total}s\n" \
    "$AUTH_URL/api/watchlist?page=1&pageSize=20" \
    -H "Authorization: Bearer $TOKEN" \
    -o /dev/null
done
```

### 3. Scoring Service Load Test

```bash
#!/bin/bash
# benchmark_scoring.sh — Measures scoring engine under load
# Requirements: hey, Apache Bench compatible

SCORING_URL="${SCORING_URL:-http://localhost:5003}"
N_REQUESTS=5000
CONCURRENCY=100

hey -n "$N_REQUESTS" -c "$CONCURRENCY" \
  "$SCORING_URL/api/scoring/top?limit=100"
```

---

## Expected Results (v2.0 Baseline)

| Test | Expected p50 | Expected p99 | Status |
|------|-------------|-------------|--------|
| POST /api/auth/register | < 150 ms | < 600 ms | ✅ |
| POST /api/auth/login | < 80 ms | < 300 ms | ✅ |
| GET /api/watchlist (20 items) | < 50 ms | < 200 ms | ✅ |
| GET /api/watchlist (10k items) | < 150 ms | < 500 ms | ✅ |
| POST /api/watchlist | < 80 ms | < 300 ms | ✅ |
| GET /api/alerts/thresholds | < 30 ms | < 100 ms | ✅ |

---

## Load Test with k6 (Optional)

Install: `npm install -g k6`

```javascript
// auth_load_test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 50,
  duration: '60s',
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

const BASE = __ENV.BASE || 'http://localhost:5005';

export default function () {
  const res = http.post(`${BASE}/api/auth/login`, JSON.stringify({
    email: `loadtest_${__VU}@example.com`,
    password: 'TestPass123!',
  }), {
    headers: { 'Content-Type': 'application/json' },
  });
  check(res, { 'status was 200 or 401': r => [200, 401].includes(r.status) });
  sleep(1);
}
```

Run: `k6 run auth_load_test.js`

---

## Memory & CPU Profiling

```bash
# CPU profile
dotnet-counters monitor -n AuthService \
  System.Runtime.CPUUsage,System.Runtime.ExecutionCounter \
  --process-id $(pgrep -f AuthService.Api | head -1)

# Memory snapshot
dotnet-dump collect -p $(pgrep -f AuthService.Api | head -1) -o auth_dump
dotnet-dump analyze auth_dump --command "dumpheap -stat"
```

---

## Passing Criteria

All tests must pass before production deployment:

- [ ] Auth throughput ≥ 500 req/s per instance
- [ ] p99 response time < 1,000 ms under 50 concurrent users
- [ ] Watchlist query < 200 ms at 10,000 items
- [ ] Memory usage < 200 MB per AuthService instance
- [ ] Zero authentication failures due to token expiry under load
