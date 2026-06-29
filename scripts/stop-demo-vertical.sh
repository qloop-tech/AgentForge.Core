#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/demo-common.sh
source "${SCRIPT_DIR}/lib/demo-common.sh"

require_command docker

vertical_id="${1:-}"
compose_project=""

if [[ -f "$DEMO_CURRENT_FILE" ]]; then
    # shellcheck disable=SC1090
    source "$DEMO_CURRENT_FILE"
fi

if [[ -n "$vertical_id" ]]; then
    compose_project="agentforge-${vertical_id}-demo"
elif [[ -n "${COMPOSE_PROJECT:-}" ]]; then
    compose_project="$COMPOSE_PROJECT"
    vertical_id="${VERTICAL_ID:-}"
else
    die "No running demo state found. Pass a vertical ID explicitly."
fi

stop_cloudflared_process

if [[ -f "${DEMO_REPO_ROOT}/artifacts/aspire-output/docker-compose.yaml" ]]; then
    (
        cd "$DEMO_REPO_ROOT"
        export AI_FOUNDRY="${AI_FOUNDRY:-dummy}"
        export COMPOSEDASHBOARDBROWSERTOKEN="${COMPOSEDASHBOARDBROWSERTOKEN:-dummy}"
        export OPENWAAPIKEY="${OPENWAAPIKEY:-dummy}"
        export OPENWAENCRYPTIONKEY="${OPENWAENCRYPTIONKEY:-dummy}"
        export OPENWAWEBHOOKSECRET="${OPENWAWEBHOOKSECRET:-dummy}"
        export OPENWAPOSTGRESPASSWORD="${OPENWAPOSTGRESPASSWORD:-dummy}"
        export OPENWA_REDIS_PASSWORD="${OPENWA_REDIS_PASSWORD:-dummy}"
        export WEBHOOK_BASE_URL="${WEBHOOK_BASE_URL:-https://${DEMO_HOSTNAME:-localhost}}"
        export WEBHOOK_HOST_PORT="${WEBHOOK_HOST_PORT:-8088}"
        export OPENWA_DASHBOARD_HOST_PORT="${OPENWA_DASHBOARD_HOST_PORT:-2886}"
        export CADDY_HTTP_HOST_PORT="${CADDY_HTTP_HOST_PORT:-8080}"
        export MCPSERVER_IMAGE="${MCPSERVER_IMAGE:-agentforge-mcpserver-local:deploytest}"
        export WEBHOOK_IMAGE="${WEBHOOK_IMAGE:-agentforge-webhook-local:deploytest}"
        export OPENWA_IMAGE="${OPENWA_IMAGE:-agentforge-openwa-local:deploytest}"
        export OPENWA_DASHBOARD_IMAGE="${OPENWA_DASHBOARD_IMAGE:-agentforge-openwa-dashboard-local:deploytest}"
        export MCPSERVER_PORT="${MCPSERVER_PORT:-8081}"
        export WEBHOOK_PORT="${WEBHOOK_PORT:-8080}"

        compose_files=(-f artifacts/aspire-output/docker-compose.yaml)
        if [[ -f artifacts/aspire-output/docker-compose.caddy.yaml ]]; then
            compose_files+=(-f artifacts/aspire-output/docker-compose.caddy.yaml)
        fi

        docker compose -p "$compose_project" "${compose_files[@]}" down --remove-orphans
    )
else
    warn "No compose file found under artifacts/aspire-output; skipped docker compose down."
fi

if [[ -f "$DEMO_CURRENT_FILE" ]]; then
    rm -f "$DEMO_CURRENT_FILE"
fi

log "Stopped demo ${vertical_id:-$compose_project}"
