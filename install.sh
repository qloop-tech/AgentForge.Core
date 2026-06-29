#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${AGENTFORGE_ENV_FILE:-${SCRIPT_DIR}/.env}"
STATE_DIR="${SCRIPT_DIR}/.agentforge"
MODE="vps"

usage() {
    cat <<'EOF'
Usage:
  ./install.sh [--mode vps|local-cloudflare]

Modes:
  vps                Runs the production Docker Compose stack behind Caddy on ports 80/443.
  local-cloudflare   Runs the same stack, with Cloudflare Tunnel forwarding to Caddy.
EOF
}

log() {
    printf '[agentforge-install] %s\n' "$*"
}

die() {
    printf '[agentforge-install] ERROR: %s\n' "$*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || die "Missing required command: $1"
}

prompt_value() {
    local name="$1"
    local prompt="$2"
    local default_value="${3:-}"
    local secret="${4:-false}"
    local current_value="${!name:-}"

    if [[ -n "$current_value" ]]; then
        return
    fi

    if [[ -n "$default_value" ]]; then
        read -r -p "${prompt} [${default_value}]: " current_value
        current_value="${current_value:-$default_value}"
    elif [[ "$secret" == "true" ]]; then
        read -r -s -p "${prompt}: " current_value
        printf '\n'
    else
        read -r -p "${prompt}: " current_value
    fi

    [[ -n "$current_value" ]] || die "${name} is required."
    export "$name=$current_value"
}

generate_secret() {
    if command -v openssl >/dev/null 2>&1; then
        openssl rand -hex 32
    else
        LC_ALL=C tr -dc 'a-f0-9' </dev/urandom | head -c 64
    fi
}

ensure_secret() {
    local name="$1"
    if [[ -z "${!name:-}" ]]; then
        export "$name=$(generate_secret)"
    fi
}

load_env() {
    if [[ -f "$ENV_FILE" ]]; then
        set -a
        # shellcheck disable=SC1090
        source "$ENV_FILE"
        set +a
    fi
}

write_env() {
    umask 077
    cat >"$ENV_FILE" <<EOF
RELEASE_VERSION=${RELEASE_VERSION}
COMPOSE_PROJECT_NAME=${COMPOSE_PROJECT_NAME}
DEPLOYMENT_MODE=${MODE}
AI_FOUNDRY='${AI_FOUNDRY}'
VERTICAL_ID=${VERTICAL_ID}
VERTICAL_PLUGIN_HOST_PATH=${VERTICAL_PLUGIN_HOST_PATH}
ASPIRE_HOSTNAME=${ASPIRE_HOSTNAME}
OPENWA_HOSTNAME=${OPENWA_HOSTNAME}
WEBHOOK_HOSTNAME=${WEBHOOK_HOSTNAME}
WEBHOOK_BASE_URL=https://${WEBHOOK_HOSTNAME}
COMPOSEDASHBOARDBROWSERTOKEN=${COMPOSEDASHBOARDBROWSERTOKEN}
OPENWAAPIKEY=${OPENWAAPIKEY}
OPENWAENCRYPTIONKEY=${OPENWAENCRYPTIONKEY}
OPENWAWEBHOOKSECRET=${OPENWAWEBHOOKSECRET}
OPENWAPOSTGRESPASSWORD=${OPENWAPOSTGRESPASSWORD}
OPENWA_REDIS_PASSWORD=${OPENWA_REDIS_PASSWORD}
MCPSERVER_IMAGE=${MCPSERVER_IMAGE}
WEBHOOK_IMAGE=${WEBHOOK_IMAGE}
OPENWA_IMAGE=${OPENWA_IMAGE}
OPENWA_DASHBOARD_IMAGE=${OPENWA_DASHBOARD_IMAGE}
MCPSERVER_PORT=${MCPSERVER_PORT}
WEBHOOK_PORT=${WEBHOOK_PORT}
WEBHOOK_HOST_PORT=${WEBHOOK_HOST_PORT}
OPENWA_DASHBOARD_HOST_PORT=${OPENWA_DASHBOARD_HOST_PORT}
CADDY_HTTP_HOST_PORT=${CADDY_HTTP_HOST_PORT}
CADDY_HTTPS_HOST_PORT=${CADDY_HTTPS_HOST_PORT}
AGENTFORGE_CADDYFILE_PATH=${AGENTFORGE_CADDYFILE_PATH}
EOF

    if [[ "$MODE" == "local-cloudflare" ]]; then
        cat >>"$ENV_FILE" <<EOF
CLOUDFLARE_TUNNEL_TOKEN=${CLOUDFLARE_TUNNEL_TOKEN}
EOF
    fi
}

write_caddyfile() {
    mkdir -p "$STATE_DIR"
    AGENTFORGE_CADDYFILE_PATH="${AGENTFORGE_CADDYFILE_PATH:-${STATE_DIR}/Caddyfile}"
    export AGENTFORGE_CADDYFILE_PATH

    if [[ "$MODE" == "local-cloudflare" ]]; then
        cat >"$AGENTFORGE_CADDYFILE_PATH" <<EOF
{
    auto_https off
}

http://${ASPIRE_HOSTNAME} {
    reverse_proxy compose-dashboard:18888
}

http://${OPENWA_HOSTNAME} {
    reverse_proxy openwa-dashboard:5000
}

http://${WEBHOOK_HOSTNAME} {
    reverse_proxy webhook:${WEBHOOK_PORT}
}
EOF
    else
        cat >"$AGENTFORGE_CADDYFILE_PATH" <<EOF
${ASPIRE_HOSTNAME} {
    reverse_proxy compose-dashboard:18888
}

${OPENWA_HOSTNAME} {
    reverse_proxy openwa-dashboard:5000
}

${WEBHOOK_HOSTNAME} {
    reverse_proxy webhook:${WEBHOOK_PORT}
}
EOF
    fi
}

compose_files() {
    printf -- '--env-file\n%s\n-f\n%s/docker-compose.yaml\n-f\n%s/docker-compose.caddy.yaml\n' "$ENV_FILE" "$SCRIPT_DIR" "$SCRIPT_DIR"
    if [[ "$MODE" == "local-cloudflare" ]]; then
        printf -- '-f\n%s/docker-compose.cloudflare.yaml\n' "$SCRIPT_DIR"
    fi
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --mode)
            MODE="${2:-}"
            shift 2
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

[[ "$MODE" == "vps" || "$MODE" == "local-cloudflare" ]] || die "Unsupported mode: ${MODE}"
[[ -f "${SCRIPT_DIR}/docker-compose.yaml" ]] || die "Missing docker-compose.yaml. Run from an AgentForge release bundle."
[[ -f "${SCRIPT_DIR}/docker-compose.caddy.yaml" ]] || die "Missing docker-compose.caddy.yaml."

require_command docker
load_env

RELEASE_VERSION="${RELEASE_VERSION:-$(cat "${SCRIPT_DIR}/VERSION" 2>/dev/null || printf 'local')}"
VERTICAL_ID="${VERTICAL_ID:-travel}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-agentforge-${VERTICAL_ID}}"
MCPSERVER_PORT="${MCPSERVER_PORT:-8081}"
WEBHOOK_PORT="${WEBHOOK_PORT:-8080}"
WEBHOOK_HOST_PORT="${WEBHOOK_HOST_PORT:-8080}"
OPENWA_DASHBOARD_HOST_PORT="${OPENWA_DASHBOARD_HOST_PORT:-2886}"
CADDY_HTTP_HOST_PORT="${CADDY_HTTP_HOST_PORT:-80}"
CADDY_HTTPS_HOST_PORT="${CADDY_HTTPS_HOST_PORT:-443}"
MCPSERVER_IMAGE="${MCPSERVER_IMAGE:-ghcr.io/qloop-tech/agentforge-core/mcpserver:${RELEASE_VERSION}}"
WEBHOOK_IMAGE="${WEBHOOK_IMAGE:-ghcr.io/qloop-tech/agentforge-core/webhook:${RELEASE_VERSION}}"
OPENWA_IMAGE="${OPENWA_IMAGE:-ghcr.io/qloop-tech/agentforge-core/openwa:${RELEASE_VERSION}}"
OPENWA_DASHBOARD_IMAGE="${OPENWA_DASHBOARD_IMAGE:-ghcr.io/qloop-tech/agentforge-core/openwa-dashboard:${RELEASE_VERSION}}"
export RELEASE_VERSION VERTICAL_ID COMPOSE_PROJECT_NAME MCPSERVER_PORT WEBHOOK_PORT WEBHOOK_HOST_PORT
export OPENWA_DASHBOARD_HOST_PORT CADDY_HTTP_HOST_PORT CADDY_HTTPS_HOST_PORT
export MCPSERVER_IMAGE WEBHOOK_IMAGE OPENWA_IMAGE OPENWA_DASHBOARD_IMAGE

prompt_value AI_FOUNDRY "Azure AI Foundry connection string" "" true
prompt_value VERTICAL_ID "Vertical id" "$VERTICAL_ID"
VERTICAL_PLUGIN_HOST_PATH="${VERTICAL_PLUGIN_HOST_PATH:-${SCRIPT_DIR}/plugins/${VERTICAL_ID}}"
prompt_value VERTICAL_PLUGIN_HOST_PATH "Published plugin folder" "$VERTICAL_PLUGIN_HOST_PATH"
prompt_value ASPIRE_HOSTNAME "Aspire dashboard hostname"
prompt_value OPENWA_HOSTNAME "OpenWA dashboard hostname"
prompt_value WEBHOOK_HOSTNAME "Webhook hostname"

if [[ "$MODE" == "local-cloudflare" ]]; then
    prompt_value CLOUDFLARE_TUNNEL_TOKEN "Cloudflare tunnel token" "" true
    export CLOUDFLARE_TUNNEL_TOKEN
fi

[[ -d "$VERTICAL_PLUGIN_HOST_PATH" ]] || die "Plugin folder not found: ${VERTICAL_PLUGIN_HOST_PATH}"
plugin_count="$(find "$VERTICAL_PLUGIN_HOST_PATH" -maxdepth 1 -name 'AgentForge.Verticals.*.dll' | wc -l | tr -d ' ')"
[[ "$plugin_count" == "1" ]] || die "Expected exactly one AgentForge.Verticals.*.dll in ${VERTICAL_PLUGIN_HOST_PATH}; found ${plugin_count}."

ensure_secret COMPOSEDASHBOARDBROWSERTOKEN
ensure_secret OPENWAAPIKEY
OPENWAAPIKEY="owa_k1_${OPENWAAPIKEY#owa_k1_}"
ensure_secret OPENWAENCRYPTIONKEY
ensure_secret OPENWAWEBHOOKSECRET
ensure_secret OPENWAPOSTGRESPASSWORD
ensure_secret OPENWA_REDIS_PASSWORD
export COMPOSEDASHBOARDBROWSERTOKEN OPENWAAPIKEY OPENWAENCRYPTIONKEY OPENWAWEBHOOKSECRET OPENWAPOSTGRESPASSWORD OPENWA_REDIS_PASSWORD
export WEBHOOK_BASE_URL="https://${WEBHOOK_HOSTNAME}"
export VERTICAL_PLUGIN_HOST_PATH ASPIRE_HOSTNAME OPENWA_HOSTNAME WEBHOOK_HOSTNAME

write_caddyfile
write_env

mapfile -t compose_args < <(compose_files)
docker compose -p "$COMPOSE_PROJECT_NAME" "${compose_args[@]}" up -d --remove-orphans

log "AgentForge installed."
log "Aspire Dashboard URL: https://${ASPIRE_HOSTNAME}/"
log "Aspire Dashboard token: ${COMPOSEDASHBOARDBROWSERTOKEN}"
log "OpenWA Dashboard URL: https://${OPENWA_HOSTNAME}/"
log "OpenWA Dashboard API key: ${OPENWAAPIKEY}"
log "OpenWA encryption key: ${OPENWAENCRYPTIONKEY}"
log "OpenWA webhook secret: ${OPENWAWEBHOOKSECRET}"
log "OpenWA Postgres password: ${OPENWAPOSTGRESPASSWORD}"
log "OpenWA Redis password: ${OPENWA_REDIS_PASSWORD}"
log "Webhook public URL: https://${WEBHOOK_HOSTNAME}"
log "Compose project: ${COMPOSE_PROJECT_NAME}"
