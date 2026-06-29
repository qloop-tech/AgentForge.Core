#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${AGENTFORGE_ENV_FILE:-${SCRIPT_DIR}/.env}"
PURGE_ALL="false"
YES="false"

usage() {
    cat <<'EOF'
Usage:
  ./uninstall.sh [--purge-all] [--yes]

Default uninstall removes AgentForge containers and network while preserving
.env, plugins, images, and Docker volumes.

Options:
  --purge-all   Also remove AgentForge-owned volumes, release images, generated config, and installer state.
  --yes         Do not prompt for confirmation.
EOF
}

log() {
    printf '[agentforge-uninstall] %s\n' "$*"
}

die() {
    printf '[agentforge-uninstall] ERROR: %s\n' "$*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || die "Missing required command: $1"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --purge-all)
            PURGE_ALL="true"
            shift
            ;;
        --yes|-y)
            YES="true"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            die "Unknown argument: $1"
            ;;
    esac
done

require_command docker

if [[ -f "$ENV_FILE" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "$ENV_FILE"
    set +a
fi

VERTICAL_ID="${VERTICAL_ID:-travel}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-agentforge-${VERTICAL_ID}}"
DEPLOYMENT_MODE="${DEPLOYMENT_MODE:-vps}"
RELEASE_VERSION="${RELEASE_VERSION:-local}"
AI_FOUNDRY="${AI_FOUNDRY:-Endpoint=https://uninstall.invalid;Key=uninstall}"
VERTICAL_PLUGIN_HOST_PATH="${VERTICAL_PLUGIN_HOST_PATH:-${SCRIPT_DIR}/plugins/${VERTICAL_ID}}"
WEBHOOK_BASE_URL="${WEBHOOK_BASE_URL:-https://webhook.localhost}"
COMPOSEDASHBOARDBROWSERTOKEN="${COMPOSEDASHBOARDBROWSERTOKEN:-uninstall-dashboard-token}"
OPENWAAPIKEY="${OPENWAAPIKEY:-owa_k1_uninstall}"
OPENWAENCRYPTIONKEY="${OPENWAENCRYPTIONKEY:-uninstall-encryption-key}"
OPENWAWEBHOOKSECRET="${OPENWAWEBHOOKSECRET:-uninstall-webhook-secret}"
OPENWAPOSTGRESPASSWORD="${OPENWAPOSTGRESPASSWORD:-uninstall-postgres-password}"
OPENWA_REDIS_PASSWORD="${OPENWA_REDIS_PASSWORD:-uninstall-redis-password}"
MCPSERVER_PORT="${MCPSERVER_PORT:-8081}"
WEBHOOK_PORT="${WEBHOOK_PORT:-8080}"
WEBHOOK_HOST_PORT="${WEBHOOK_HOST_PORT:-8080}"
OPENWA_DASHBOARD_HOST_PORT="${OPENWA_DASHBOARD_HOST_PORT:-2886}"
CADDY_HTTP_HOST_PORT="${CADDY_HTTP_HOST_PORT:-80}"
CADDY_HTTPS_HOST_PORT="${CADDY_HTTPS_HOST_PORT:-443}"
ASPIRE_HOSTNAME="${ASPIRE_HOSTNAME:-aspire.localhost}"
OPENWA_HOSTNAME="${OPENWA_HOSTNAME:-openwa.localhost}"
WEBHOOK_HOSTNAME="${WEBHOOK_HOSTNAME:-webhook.localhost}"
AGENTFORGE_CADDYFILE_PATH="${AGENTFORGE_CADDYFILE_PATH:-${SCRIPT_DIR}/.agentforge/Caddyfile}"
CLOUDFLARE_TUNNEL_TOKEN="${CLOUDFLARE_TUNNEL_TOKEN:-uninstall-cloudflare-token}"
MCPSERVER_IMAGE="${MCPSERVER_IMAGE:-ghcr.io/qloop-tech/agentforge-core/mcpserver:${RELEASE_VERSION}}"
WEBHOOK_IMAGE="${WEBHOOK_IMAGE:-ghcr.io/qloop-tech/agentforge-core/webhook:${RELEASE_VERSION}}"
OPENWA_IMAGE="${OPENWA_IMAGE:-ghcr.io/qloop-tech/agentforge-core/openwa:${RELEASE_VERSION}}"
OPENWA_DASHBOARD_IMAGE="${OPENWA_DASHBOARD_IMAGE:-ghcr.io/qloop-tech/agentforge-core/openwa-dashboard:${RELEASE_VERSION}}"

export RELEASE_VERSION AI_FOUNDRY VERTICAL_ID VERTICAL_PLUGIN_HOST_PATH WEBHOOK_BASE_URL
export COMPOSEDASHBOARDBROWSERTOKEN OPENWAAPIKEY OPENWAENCRYPTIONKEY OPENWAWEBHOOKSECRET
export OPENWAPOSTGRESPASSWORD OPENWA_REDIS_PASSWORD MCPSERVER_PORT WEBHOOK_PORT WEBHOOK_HOST_PORT
export OPENWA_DASHBOARD_HOST_PORT CADDY_HTTP_HOST_PORT CADDY_HTTPS_HOST_PORT ASPIRE_HOSTNAME
export OPENWA_HOSTNAME WEBHOOK_HOSTNAME AGENTFORGE_CADDYFILE_PATH CLOUDFLARE_TUNNEL_TOKEN
export MCPSERVER_IMAGE WEBHOOK_IMAGE OPENWA_IMAGE OPENWA_DASHBOARD_IMAGE

[[ "$COMPOSE_PROJECT_NAME" == agentforge* ]] || die "Refusing to uninstall unexpected Compose project '${COMPOSE_PROJECT_NAME}'."

compose_files=()
[[ -f "$ENV_FILE" ]] && compose_files+=(--env-file "$ENV_FILE")
compose_files+=(-f "$SCRIPT_DIR/docker-compose.yaml")
[[ -f "$SCRIPT_DIR/docker-compose.caddy.yaml" ]] && compose_files+=(-f "$SCRIPT_DIR/docker-compose.caddy.yaml")
if [[ "$DEPLOYMENT_MODE" == "local-cloudflare" && -f "$SCRIPT_DIR/docker-compose.cloudflare.yaml" ]]; then
    compose_files+=(-f "$SCRIPT_DIR/docker-compose.cloudflare.yaml")
fi

log "Compose project: ${COMPOSE_PROJECT_NAME}"
if [[ "$PURGE_ALL" == "true" ]]; then
    log "Mode: purge-all. Containers, network, volumes, images, .env, and generated state will be removed."
else
    log "Mode: default. Containers and network will be removed; volumes, images, .env, and plugins stay."
fi

if [[ "$YES" != "true" ]]; then
    read -r -p "Continue? Type 'yes' to confirm: " answer
    [[ "$answer" == "yes" ]] || die "Cancelled."
fi

if [[ -f "$SCRIPT_DIR/docker-compose.yaml" ]]; then
    if [[ "$PURGE_ALL" == "true" ]]; then
        docker compose -p "$COMPOSE_PROJECT_NAME" "${compose_files[@]}" down -v --remove-orphans || true
    else
        docker compose -p "$COMPOSE_PROJECT_NAME" "${compose_files[@]}" down --remove-orphans || true
    fi
else
    log "No docker-compose.yaml found; skipping Compose down."
fi

if [[ "$PURGE_ALL" == "true" ]]; then
    images=(
        "${MCPSERVER_IMAGE:-}"
        "${WEBHOOK_IMAGE:-}"
        "${OPENWA_IMAGE:-}"
        "${OPENWA_DASHBOARD_IMAGE:-}"
    )

    for image in "${images[@]}"; do
        if [[ -n "$image" && "$image" == ghcr.io/qloop-tech/agentforge-core/* ]]; then
            docker image rm "$image" >/dev/null 2>&1 || true
        fi
    done

    rm -rf "$SCRIPT_DIR/.agentforge"
    rm -f "$ENV_FILE"
fi

log "Uninstall complete."
