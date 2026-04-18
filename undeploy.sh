#!/usr/bin/env bash
# ==============================================================================
# CrossMarket Price Analyzer — Undeploy Script
# ==============================================================================
# Usage: ./undeploy.sh [--force] [--help]
#
# Options:
#   --force   Skip confirmation prompt
#   --help    Show this help message
#
# Removes: containers, images, volumes, networks, build cache
# ==============================================================================

set -euo pipefail

# ── Colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

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
FORCE=false
for arg in "$@"; do
  case "$arg" in
    --force|-y) FORCE=true ;;
    --help|-h)  usage ;;
    *)          die "Unknown option: $arg" ;;
  esac
done

# ── Locate project root ───────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ── Confirmation ─────────────────────────────────────────────────────────────
if [[ "$FORCE" != "true" ]]; then
  echo
  echo -e "${RED}⚠  WARNING: This will remove ALL of the following:${NC}"
  echo
  echo "  • All running containers  (product-api, matching-api, scoring-api,"
  echo "                            notification-api, scraping-worker,"
  echo "                            gateway, frontend, mysql, redis,"
  echo "                            rabbitmq, prometheus, grafana)"
  echo "  • All custom images built by docker compose"
  echo "  • All named volumes       (mysql_data, redis_data, rabbitmq_data,"
  echo "                            grafana_data, prometheus_data)"
  echo "  • Docker build cache"
  echo "  • Docker networks created by this compose stack"
  echo
  echo -e "${YELLOW}⚠  ALL DATA WILL BE LOST.${NC}"
  echo
  read -rp "  Are you sure? Type 'yes' to confirm: " confirm
  if [[ "$confirm" != "yes" ]]; then
    echo "Aborted."
    exit 0
  fi
fi

# ── Stop & remove containers ──────────────────────────────────────────────────
info "Stopping and removing containers..."
docker compose down --remove-orphans 2>/dev/null || true
success "Containers removed."

# ── Remove custom images ─────────────────────────────────────────────────────
info "Removing custom images..."

# Collect image names/repo:tag entries from docker-compose.yml
mapfile -t COMPOSE_IMAGES < <( \
  docker compose config --images 2>/dev/null | grep -v '^$' || true \
)

if [[ ${#COMPOSE_IMAGES[@]} -gt 0 ]]; then
  for img in "${COMPOSE_IMAGES[@]}"; do
    if [[ -n "$img" ]]; then
      info "  Removing image: $img"
      docker rmi -f "$img" 2>/dev/null || true
    fi
  done
else
  # Fallback: remove by known prefixes
  info "Removing known CMA images by prefix..."
  docker images --format '{{.Repository}}:{{.Tag}}' | while read -r img; do
    case "$img" in
      cma-*)          docker rmi -f "$img" 2>/dev/null || true ;;
      cma/*)          docker rmi -f "$img" 2>/dev/null || true ;;
    esac
  done
fi
success "Custom images removed."

# ── Remove infrastructure images (optional) ───────────────────────────────────
info "Removing infrastructure images..."
infra_images=("mysql:8.0" "redis:7-alpine" "rabbitmq:3.12-management-alpine" "prom/prometheus:latest" "grafana/grafana:latest")
for img in "${infra_images[@]}"; do
  docker rmi -f "$img" 2>/dev/null || true
done
success "Infrastructure images removed (if not used by other projects)."

# ── Remove named volumes ───────────────────────────────────────────────────────
info "Removing named volumes..."
docker volume rm \
  "$(docker volume ls -qf name=cma_mysql_data 2>/dev/null || true)" \
  "$(docker volume ls -qf name=cma_redis_data 2>/dev/null || true)" \
  "$(docker volume ls -qf name=cma_rabbitmq_data 2>/dev/null || true)" \
  "$(docker volume ls -qf name=cma_grafana_data 2>/dev/null || true)" \
  "$(docker volume ls -qf name=cma_prometheus_data 2>/dev/null || true)" \
  2>/dev/null || true

# Also remove any dangling CMA volumes regardless of prefix
docker volume ls -qf "name=cma" | while read -r vol; do
  info "  Removing volume: $vol"
  docker volume rm "$vol" 2>/dev/null || true
done
success "Volumes removed."

# ── Prune build cache ─────────────────────────────────────────────────────────
info "Pruning Docker build cache..."
docker builder prune -af --filter "label=com.docker.compose.project=crossmarket_price_analyzer" 2>/dev/null || \
  docker builder prune -af 2>/dev/null || true
docker system prune -f --filter "label=com.docker.compose.project=crossmarket_price_analyzer" 2>/dev/null || true
success "Build cache pruned."

# ── Remove networks ────────────────────────────────────────────────────────────
info "Removing Docker networks..."
docker network ls --format '{{.Name}}' | grep -E '^cma[-_]|^crossmarket[-_]price[-_]analyzer[-_]' | while read -r net; do
  info "  Removing network: $net"
  docker network rm "$net" 2>/dev/null || true
done
success "Networks removed."

# ── Final cleanup ─────────────────────────────────────────────────────────────
info "Running final system prune..."
docker system prune -f 2>/dev/null || true

# ── Summary ───────────────────────────────────────────────────────────────────
echo
echo "═══════════════════════════════════════════════════════════════"
echo "  CrossMarket Price Analyzer — Fully Undeployed"
echo "═══════════════════════════════════════════════════════════════"
echo "  ✅ Containers removed"
echo "  ✅ Custom images removed"
echo "  ✅ Infrastructure images removed"
echo "  ✅ Volumes (data) removed"
echo "  ✅ Build cache pruned"
echo "  ✅ Networks removed"
echo "═══════════════════════════════════════════════════════════════"
echo
success "To redeploy: ./deploy.sh"
echo