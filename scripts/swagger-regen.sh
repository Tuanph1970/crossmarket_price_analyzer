#!/usr/bin/env bash
# scripts/swagger-regen.sh
# P3-T03: Regenerates Swashbuckle XML documentation files for all Phase 3 services.
# Run this after adding new endpoints or modifying DTOs.
#
# Usage:
#   ./scripts/swagger-regen.sh          # regenerate all services
#   ./scripts/swagger-regen.sh scoring  # regenerate only ScoringService

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$ROOT_DIR/src/Services"

SERVICES=(
  "ProductService/ProductService.Api"
  "MatchingService/MatchingService.Api"
  "ScoringService/ScoringService.Api"
  "NotificationService/NotificationService.Api"
  "CMA.Gateway"
)

generate_for_service() {
  local svc_path="$1"
  local svc_name
  svc_name=$(basename "$svc_path")
  local proj_file="$svc_path/${svc_name}.Api.csproj"
  local xml_file="$svc_path/${svc_name}.Api/${svc_name}.Api.xml"
  local out_dir="$svc_path/${svc_name}.Api/bin/Release/net9.0"

  if [[ ! -f "$proj_file" ]]; then
    echo "⚠️  Skipping $proj_file — not found"
    return 0
  fi

  echo "📄 Generating XML docs for $svc_name..."
  dotnet build "$proj_file" -c Release --no-incremental -p:GenerateDocumentationFile=true

  if [[ -f "$xml_file" ]]; then
    echo "✅ $svc_name XML docs: $xml_file ($(wc -c < "$xml_file") bytes)"
  else
    echo "❌ $svc_name XML docs not found at $xml_file"
  fi
}

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$HOME/.dotnet}"
export PATH="$DOTNET_CLI_HOME:$PATH"

echo "=== Swagger XML Documentation Regeneration ==="
echo ""

if [[ $# -eq 0 ]]; then
  for svc in "${SERVICES[@]}"; do
    generate_for_service "$BUILD_DIR/$svc"
  done
else
  case "$1" in
    scoring)  generate_for_service "$BUILD_DIR/ScoringService/ScoringService.Api" ;;
    product)  generate_for_service "$BUILD_DIR/ProductService/ProductService.Api" ;;
    matching) generate_for_service "$BUILD_DIR/MatchingService/MatchingService.Api" ;;
    notify)   generate_for_service "$BUILD_DIR/NotificationService/NotificationService.Api" ;;
    gateway)  generate_for_service "$BUILD_DIR/CMA.Gateway" ;;
    *)        echo "Unknown service: $1"; exit 1 ;;
  esac
fi

echo ""
echo "=== Done ==="
echo "XML docs are embedded in each service's DLL and served at /swagger/v1/swagger.json"
