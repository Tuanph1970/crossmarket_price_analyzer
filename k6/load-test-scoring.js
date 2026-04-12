import http from 'k6/http';
import { sleep, check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// ── Custom metrics ────────────────────────────────────────────────────────────
const errorRate = new Rate('errors');
const latency = new Trend('latency_ms');

// ── Test configuration ───────────────────────────────────────────────────────
export const options = {
  // Ramp to 50 VUs over 2 min, hold at 50 for 3 min, drop over 1 min
  stages: [
    { duration: '2m', target: 50 },   // ramp up
    { duration: '3m', target: 50 },   // steady state
    { duration: '1m', target: 0 },    // ramp down
  ],
  thresholds: {
    // p95 < 500ms, p99 < 1s
    'latency_ms': ['p(95)<500', 'p(99)<1000'],
    // < 1% error rate
    'errors': ['rate<0.01'],
    // < 200ms avg
    'latency_ms': ['avg<200'],
  },
};

// ── Base URLs (override with environment variables) ───────────────────────────
const GATEWAY_URL = __ENV.GATEWAY_URL || 'http://localhost:8080';
const SCORING_URL = __ENV.SCORING_URL || 'http://localhost:5003';

// ── Helper ────────────────────────────────────────────────────────────────────
const headers = {
  'Accept': 'application/json',
  'Content-Type': 'application/json',
};

export function setup() {
  // Warm-up: fetch scores so caches are primed
  http.get(`${SCORING_URL}/api/scores?page=1&pageSize=20`, { headers });
  sleep(1);
  return {};
}

export default function () {
  const scenarios = [
    // ── GET /api/scores (paginated) ──────────────────────────────────────────
    () => {
      const res = http.get(`${SCORING_URL}/api/scores?page=1&pageSize=20`, { headers });
      latency.add(res.timings.duration);
      check(res, {
        'GET /api/scores — status 200': (r) => r.status === 200,
        'GET /api/scores — body not empty': (r) => r.json('items')?.length > 0,
      }) || errorRate.add(1);
    },

    // ── GET /api/scores/config ────────────────────────────────────────────────
    () => {
      const res = http.get(`${SCORING_URL}/api/scores/config`, { headers });
      latency.add(res.timings.duration);
      check(res, {
        'GET /api/scores/config — status 200': (r) => r.status === 200,
      }) || errorRate.add(1);
    },

    // ── GET /api/scores/websocket/health ───────────────────────────────────────
    () => {
      const res = http.get(`${SCORING_URL}/api/scores/websocket/health`, { headers });
      latency.add(res.timings.duration);
      check(res, {
        'GET websocket/health — status 200': (r) => r.status === 200,
        'GET websocket/health — connectionCount present': (r) => r.json('totalConnections') !== undefined,
      }) || errorRate.add(1);
    },

    // ── GET /api/scores (filtered) ─────────────────────────────────────────────
    () => {
      const res = http.get(`${SCORING_URL}/api/scores?page=1&pageSize=20&minMargin=20`, { headers });
      latency.add(res.timings.duration);
      check(res, {
        'GET /api/scores?minMargin=20 — status 200': (r) => r.status === 200,
      }) || errorRate.add(1);
    },

    // ── POST /api/scores/recalculate ──────────────────────────────────────────
    () => {
      const res = http.post(`${SCORING_URL}/api/scores/recalculate`, null, { headers });
      latency.add(res.timings.duration);
      check(res, {
        'POST /api/scores/recalculate — status 200': (r) => r.status === 200,
      }) || errorRate.add(1);
    },

    // ── GET /health (ScoringService health) ────────────────────────────────────
    () => {
      const res = http.get(`${SCORING_URL}/health`, { headers });
      latency.add(res.timings.duration);
      check(res, {
        'GET /health — status 200': (r) => r.status === 200,
        'GET /health — healthy': (r) => r.json('status') === 'Healthy',
      }) || errorRate.add(1);
    },
  ];

  // Execute one random scenario per VU iteration to simulate varied traffic
  const scenarioIndex = Math.floor(Math.random() * scenarios.length);
  scenarios[scenarioIndex]();

  // Simulate realistic think time (2–6 seconds between requests)
  sleep(Math.random() * 4 + 2);
}

export function handleSummary(data) {
  return {
    'stdout': textSummary(data),
    'k6-results.json': JSON.stringify(data, null, 2),
  };
}

function textSummary(data) {
  const httpData = data.metrics.http_req_duration;
  return `
=== CrossMarket Price Analyzer — k6 Load Test Summary ===
Time:        ${new Date().toISOString()}

HTTP Response Times (ms):
  avg   : ${httpData.values.avg.toFixed(2)}
  p50   : ${httpData.values['p(50)'].toFixed(2)}
  p95   : ${httpData.values['p(95)'].toFixed(2)}
  p99   : ${httpData.values['p(99)'].toFixed(2)}
  max   : ${httpData.values.max.toFixed(2)}

HTTP Reqs    : ${data.metrics.http_reqs?.values?.passes ?? 0} passed, ${data.metrics.http_reqs?.values?.fails ?? 0} failed
Error Rate   : ${((data.metrics.errors?.values?.rate ?? 0) * 100).toFixed(2)}%
VUs (max)    : ${data.state?.vus ?? '?'}
Duration     : ${(data.state?.test_run_duration_ms ?? 0) / 1000}s
  `;
}