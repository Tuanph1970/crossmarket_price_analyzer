#!/usr/bin/env bash
# ==============================================================================
# CrossMarket Price Analyzer — Deploy Script
# ==============================================================================
# Usage: ./deploy.sh [--no-build] [--observability] [--help]
#
# Options:
#   --no-build       Skip image build (use existing images)
#   --observability  Also start Prometheus + Grafana
#   --help           Show this help message
# ==============================================================================

set -euo pipefail

# ── Colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Colour

# ── Defaults ──────────────────────────────────────────────────────────────────
SKIP_BUILD=false
WITH_OBSERVABILITY=false

# ── Helpers ───────────────────────────────────────────────────────────────────
info()    { echo -e "${CYAN}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC}   $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
fail()    { echo -e "${RED}[FAIL]${NC} $*" >&2; }

die()     { fail "$*"; exit 1; }

usage() {
  grep '^#' "$0" | sed 's/^#//'
  exit 0
}

# ── Argument parsing ──────────────────────────────────────────────────────────
for arg in "$@"; do
  case "$arg" in
    --no-build)      SKIP_BUILD=true ;;
    --observability) WITH_OBSERVABILITY=true ;;
    --help|-h)       usage ;;
    *)               die "Unknown option: $arg" ;;
  esac
done

# ── Pre-flight checks ─────────────────────────────────────────────────────────
info "Pre-flight checks..."

if ! command -v docker &>/dev/null; then
  die "Docker is not installed or not in PATH."
fi

# Navigate to project root (assume script is at repo root)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [[ ! -f docker-compose.yml ]]; then
  die "docker-compose.yml not found in $SCRIPT_DIR"
fi

success "Pre-flight checks passed."

# ── Build step ─────────────────────────────────────────────────────────────────
if [[ "$SKIP_BUILD" == "false" ]]; then
  info "Building Docker images..."
  echo

  if [[ "$WITH_OBSERVABILITY" == "true" ]]; then
    docker compose build --parallel
  else
    docker compose build --parallel --exclude-scopes observability
  fi

  success "All images built successfully."
else
  info "Skipping image build (--no-build specified)."
fi

# ── Start infrastructure first ─────────────────────────────────────────────────
info "Starting infrastructure services (MySQL, Redis, RabbitMQ)..."
docker compose up -d mysql redis rabbitmq

# Wait for infrastructure to be healthy
info "Waiting for MySQL to be healthy..."
for i in {1..30}; do
  if docker exec cma-mysql mysqladmin ping -h localhost --silent 2>/dev/null; then
    success "MySQL is up."
    break
  fi
  echo -n "."
  sleep 2
done

info "Waiting for Redis to be healthy..."
for i in {1..15}; do
  if docker exec cma-redis redis-cli ping &>/dev/null; then
    success "Redis is up."
    break
  fi
  echo -n "."
  sleep 2
done

info "Waiting for RabbitMQ to be healthy..."
for i in {1..15}; do
  if docker exec cma-rabbitmq rabbitmq-diagnostics ping &>/dev/null; then
    success "RabbitMQ is up."
    break
  fi
  echo -n "."
  sleep 2
done

# ── Start API services ─────────────────────────────────────────────────────────
info "Starting API services..."
docker compose up -d \
  product-api \
  matching-api \
  scoring-api \
  notification-api \
  scraping-worker \
  gateway \
  frontend

# ── Observability (optional) ───────────────────────────────────────────────────
if [[ "$WITH_OBSERVABILITY" == "true" ]]; then
  info "Starting observability stack (Prometheus + Grafana)..."
  docker compose --profile observability up -d prometheus grafana
fi

# ── Health check ───────────────────────────────────────────────────────────────
echo
info "Running health checks..."

HEALTHY=0
TOTAL=8
[[ "$WITH_OBSERVABILITY" == "true" ]] && ((TOTAL+=2))

services=(
  cma-mysql
  cma-redis
  cma-rabbitmq
  cma-product-api
  cma-matching-api
  cma-scoring-api
  cma-notification-api
  cma-scraping-worker
  cma-gateway
  cma-frontend
)

for svc in "${services[@]}"; do
  for i in {1..10}; do
    if docker inspect --format='{{.State.Health.Status}}' "$svc" 2>/dev/null | grep -q "healthy"; then
      success "$svc is healthy"
      ((HEALTHY++))
      break
    elif docker inspect --format='{{.State.Running}}' "$svc" 2>/dev/null | grep -q "true"; then
      # Service running but no healthcheck defined (e.g. frontend) — count as healthy
      success "$svc is running"
      ((HEALTHY++))
      break
    fi
    sleep 3
  done
done

echo
if [[ "$HEALTHY" -ge "$TOTAL" ]]; then
  success "All $HEALTHY services are up!"
else
  warn "$HEALTHY/$TOTAL services confirmed — some may still be starting up."
fi

# ── Summary ────────────────────────────────────────────────────────────────────
echo
echo "═══════════════════════════════════════════════════════════════"
echo "  CrossMarket Price Analyzer — Deployed"
echo "═══════════════════════════════════════════════════════════════"
echo "  Gateway      http://localhost:8080"
echo "  Frontend     http://localhost:3000"
echo "  Product API  http://localhost:5001"
echo "  Matching API http://localhost:5002"
echo "  Scoring API  http://localhost:5003"
echo "  Notification http://localhost:5004"
echo "  RabbitMQ     http://localhost:15672  (guest/guest)"
echo "  Redis        localhost:6379"
if [[ "$WITH_OBSERVABILITY" == "true" ]]; then
  echo "  Prometheus   http://localhost:9090"
  echo "  Grafana      http://localhost:3001  (admin/admin)"
fi
echo "═══════════════════════════════════════════════════════════════"
echo
info "To view logs:       docker compose logs -f [service]"
info "To stop:            docker compose down"
info "To undeploy:        ./undeploy.sh"
echo