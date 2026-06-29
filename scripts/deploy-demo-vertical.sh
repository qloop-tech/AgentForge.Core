#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/demo-common.sh
source "${SCRIPT_DIR}/lib/demo-common.sh"

require_cloudflared
require_command docker
require_command dotnet
require_command aspire
require_command curl
load_demo_state
load_runtime_env_file_if_present

get_apphost_secret() {
    local key="$1"
    dotnet user-secrets list --project "${DEMO_REPO_ROOT}/src/AgentForge.AppHost/AgentForge.AppHost.csproj" \
        | sed -n "s/^${key} = //p" \
        | tail -n 1
}

vertical_id="${1:-}"
vertical_id="$(prompt_for_value "Vertical ID" "$vertical_id")"
[[ -n "$vertical_id" ]] || die "Vertical ID is required."

customer_config_source_path="${CUSTOMER_CONFIG_SOURCE_PATH:-}"
webhook_host_port="${WEBHOOK_HOST_PORT:-8088}"
openwa_dashboard_host_port="${OPENWA_DASHBOARD_HOST_PORT:-2886}"
caddy_http_host_port="${CADDY_HTTP_HOST_PORT:-8080}"
compose_project="${COMPOSE_PROJECT_NAME:-agentforge-${vertical_id}-demo}"
hostname="${DEMO_HOSTNAME_OVERRIDE:-${vertical_id}-demo.${DEMO_ROOT_DOMAIN}}"
openwa_hostname="${OPENWA_DEMO_HOSTNAME_OVERRIDE:-${vertical_id}-openwa-demo.${DEMO_ROOT_DOMAIN}}"
aspire_hostname="${ASPIRE_DEMO_HOSTNAME_OVERRIDE:-${vertical_id}-aspire-demo.${DEMO_ROOT_DOMAIN}}"

MCPSERVER_IMAGE="${MCPSERVER_IMAGE:-agentforge-mcpserver-local:deploytest}"
WEBHOOK_IMAGE="${WEBHOOK_IMAGE:-agentforge-webhook-local:deploytest}"
OPENWA_IMAGE="${OPENWA_IMAGE:-agentforge-openwa-local:deploytest}"
OPENWA_DASHBOARD_IMAGE="${OPENWA_DASHBOARD_IMAGE:-agentforge-openwa-dashboard-local:deploytest}"
COMPOSEDASHBOARDBROWSERTOKEN="${COMPOSEDASHBOARDBROWSERTOKEN:-}"
AI_FOUNDRY="${AI_FOUNDRY:-}"
OPENWAAPIKEY="${OPENWAAPIKEY:-}"
OPENWAENCRYPTIONKEY="${OPENWAENCRYPTIONKEY:-}"
OPENWAWEBHOOKSECRET="${OPENWAWEBHOOKSECRET:-}"
OPENWAPOSTGRESPASSWORD="${OPENWAPOSTGRESPASSWORD:-}"
OPENWA_REDIS_PASSWORD="${OPENWA_REDIS_PASSWORD:-}"
MCPSERVER_PORT="${MCPSERVER_PORT:-8081}"
WEBHOOK_PORT="${WEBHOOK_PORT:-8080}"
WEBHOOK_BASE_URL="https://${hostname}"
OPENWA_DASHBOARD_HOST_PORT="${openwa_dashboard_host_port}"
CADDY_HTTP_HOST_PORT="${caddy_http_host_port}"

AI_FOUNDRY="${AI_FOUNDRY:-$(get_apphost_secret "ConnectionStrings:ai-foundry")}"
COMPOSEDASHBOARDBROWSERTOKEN="${COMPOSEDASHBOARDBROWSERTOKEN:-$(get_apphost_secret "AppHost:DashboardApiKey")}"
OPENWAAPIKEY="${OPENWAAPIKEY:-$(get_apphost_secret "Parameters:openWaApiKey")}"
OPENWAENCRYPTIONKEY="${OPENWAENCRYPTIONKEY:-$(get_apphost_secret "Parameters:openWaEncryptionKey")}"
OPENWAWEBHOOKSECRET="${OPENWAWEBHOOKSECRET:-$(get_apphost_secret "Parameters:openWaWebhookSecret")}"
OPENWAPOSTGRESPASSWORD="${OPENWAPOSTGRESPASSWORD:-$(get_apphost_secret "Parameters:openWaPostgresPassword")}"
OPENWA_REDIS_PASSWORD="${OPENWA_REDIS_PASSWORD:-$(get_apphost_secret "Parameters:openwa-redis-password")}"

require_env_var AI_FOUNDRY
require_env_var COMPOSEDASHBOARDBROWSERTOKEN
require_env_var OPENWAAPIKEY
require_env_var OPENWAENCRYPTIONKEY
require_env_var OPENWAWEBHOOKSECRET
require_env_var OPENWAPOSTGRESPASSWORD
require_env_var OPENWA_REDIS_PASSWORD
require_env_var MCPSERVER_IMAGE
require_env_var WEBHOOK_IMAGE
require_env_var OPENWA_IMAGE
require_env_var OPENWA_DASHBOARD_IMAGE
require_env_var MCPSERVER_PORT
require_env_var WEBHOOK_PORT
require_env_var WEBHOOK_HOST_PORT
require_env_var OPENWA_DASHBOARD_HOST_PORT
require_env_var CADDY_HTTP_HOST_PORT

if [[ "$webhook_host_port" == "$openwa_dashboard_host_port" ]]; then
    die "WEBHOOK_HOST_PORT (${webhook_host_port}) and OPENWA_DASHBOARD_HOST_PORT (${openwa_dashboard_host_port}) must be different."
fi

if [[ "$webhook_host_port" == "$caddy_http_host_port" ]]; then
    die "WEBHOOK_HOST_PORT (${webhook_host_port}) and CADDY_HTTP_HOST_PORT (${caddy_http_host_port}) must be different."
fi

if [[ "$hostname" == "$openwa_hostname" ]]; then
    die "The webhook hostname and OpenWA hostname must be different."
fi

if [[ "$hostname" == "$aspire_hostname" || "$openwa_hostname" == "$aspire_hostname" ]]; then
    die "The Aspire, webhook, and OpenWA hostnames must be different."
fi

vertical_project="${VERTICAL_PROJECT_PATH:-$(find_vertical_project "$vertical_id")}"
plugin_output_dir="${DEMO_REPO_ROOT}/artifacts/plugins/${vertical_id}"
compose_output_dir="${DEMO_REPO_ROOT}/artifacts/aspire-output"
caddyfile_path="${compose_output_dir}/Caddyfile"
caddy_compose_path="${compose_output_dir}/docker-compose.caddy.yaml"
cloudflared_compose_config_path="${compose_output_dir}/cloudflared-demo.compose.yml"

parse_image_reference "$MCPSERVER_IMAGE" "deploytest"
mcp_repository="$IMAGE_REPOSITORY"
mcp_tag="$IMAGE_TAG"

parse_image_reference "$WEBHOOK_IMAGE" "deploytest"
webhook_repository="$IMAGE_REPOSITORY"
webhook_tag="$IMAGE_TAG"

parse_image_reference "$OPENWA_IMAGE" "deploytest"
openwa_repository="$IMAGE_REPOSITORY"
openwa_tag="$IMAGE_TAG"

parse_image_reference "$OPENWA_DASHBOARD_IMAGE" "deploytest"
openwa_dashboard_repository="$IMAGE_REPOSITORY"
openwa_dashboard_tag="$IMAGE_TAG"

log "Publishing vertical plugin ${vertical_id}"
dotnet publish "$vertical_project" -c Release -o "$plugin_output_dir"

log "Building container image ${MCPSERVER_IMAGE}"
dotnet publish "${DEMO_REPO_ROOT}/src/AgentForge.McpHost/AgentForge.McpHost.csproj" \
    -c Release \
    /t:PublishContainer \
    -p:ContainerRepository="$mcp_repository" \
    -p:ContainerImageTag="$mcp_tag"

log "Building container image ${WEBHOOK_IMAGE}"
dotnet publish "${DEMO_REPO_ROOT}/src/AgentForge.WebApi/AgentForge.WebApi.csproj" \
    -c Release \
    /t:PublishContainer \
    -p:ContainerRepository="$webhook_repository" \
    -p:ContainerImageTag="$webhook_tag"

log "Publishing Aspire compose output"
(
    cd "$DEMO_REPO_ROOT"
    export VERTICAL_ID="$vertical_id"
    export VERTICAL_PLUGIN_SOURCE_PATH="$plugin_output_dir"
    if [[ -n "$customer_config_source_path" ]]; then
        export CUSTOMER_CONFIG_SOURCE_PATH="$customer_config_source_path"
    fi
    aspire publish --apphost src/AgentForge.AppHost/AgentForge.AppHost.csproj -o "$compose_output_dir"
)

log "Writing Caddy ingress configuration"
write_file_atomically "$caddyfile_path" <<EOF
{
    auto_https off
}

http://${aspire_hostname} {
    reverse_proxy compose-dashboard:18888
}

http://${openwa_hostname} {
    reverse_proxy openwa-dashboard:5000
}

http://${hostname} {
    reverse_proxy webhook:${WEBHOOK_PORT}
}
EOF

log "Ensuring DNS route for ${hostname}"
cloudflared tunnel route dns --overwrite-dns "$CLOUDFLARE_TUNNEL_NAME" "$hostname"
log "Ensuring DNS route for ${openwa_hostname}"
cloudflared tunnel route dns --overwrite-dns "$CLOUDFLARE_TUNNEL_NAME" "$openwa_hostname"
log "Ensuring DNS route for ${aspire_hostname}"
cloudflared tunnel route dns --overwrite-dns "$CLOUDFLARE_TUNNEL_NAME" "$aspire_hostname"

credentials_file="${DEMO_CLOUDFLARED_DIR}/${CLOUDFLARE_TUNNEL_ID}.json"
[[ -f "$credentials_file" ]] || die "Missing tunnel credentials file: ${credentials_file}"

write_file_atomically "$DEMO_TUNNEL_CONFIG" <<EOF
tunnel: ${CLOUDFLARE_TUNNEL_ID}
credentials-file: ${credentials_file}
ingress:
  # Cloudflared matches ingress rules in order; keep the catch-all 404 handler last.
  - hostname: ${hostname}
    service: http://localhost:${caddy_http_host_port}
  - hostname: ${openwa_hostname}
    service: http://localhost:${caddy_http_host_port}
  - hostname: ${aspire_hostname}
    service: http://localhost:${caddy_http_host_port}
  - service: http_status:404
EOF

write_file_atomically "$cloudflared_compose_config_path" <<EOF
tunnel: ${CLOUDFLARE_TUNNEL_ID}
credentials-file: /etc/cloudflared/${CLOUDFLARE_TUNNEL_ID}.json
ingress:
  # Cloudflared and Caddy share the Compose network in Mac mini mode.
  - hostname: ${hostname}
    service: http://caddy:80
  - hostname: ${openwa_hostname}
    service: http://caddy:80
  - hostname: ${aspire_hostname}
    service: http://caddy:80
  - service: http_status:404
EOF

cloudflared tunnel --config "$DEMO_TUNNEL_CONFIG" ingress validate

write_file_atomically "$caddy_compose_path" <<EOF
services:
  caddy:
    image: docker.io/library/caddy:2-alpine
    restart: unless-stopped
    ports:
      - "\${CADDY_HTTP_HOST_PORT:-8080}:80"
    volumes:
      - type: bind
        source: ${caddyfile_path}
        target: /etc/caddy/Caddyfile
        read_only: true
    depends_on:
      compose-dashboard:
        condition: service_started
      openwa-dashboard:
        condition: service_started
      webhook:
        condition: service_started
    networks:
      - aspire

  cloudflared:
    image: docker.io/cloudflare/cloudflared:latest
    restart: unless-stopped
    command: tunnel --config /etc/cloudflared/config.yml run ${CLOUDFLARE_TUNNEL_NAME}
    volumes:
      - type: bind
        source: ${cloudflared_compose_config_path}
        target: /etc/cloudflared/config.yml
        read_only: true
      - type: bind
        source: ${credentials_file}
        target: /etc/cloudflared/${CLOUDFLARE_TUNNEL_ID}.json
        read_only: true
    depends_on:
      caddy:
        condition: service_started
    networks:
      - aspire
EOF

log "Building container image ${OPENWA_IMAGE}"
docker build \
    -f "${compose_output_dir}/openwa.Dockerfile" \
    -t "${openwa_repository}:${openwa_tag}" \
    "${DEMO_REPO_ROOT}/src/OpenWA"

log "Building container image ${OPENWA_DASHBOARD_IMAGE}"
docker build \
    -f "${compose_output_dir}/openwa-dashboard.Dockerfile" \
    -t "${openwa_dashboard_repository}:${openwa_dashboard_tag}" \
    "${DEMO_REPO_ROOT}/src/OpenWA/dashboard"

stop_cloudflared_process

log "Starting Docker Compose project ${compose_project}"
(
    cd "$DEMO_REPO_ROOT"
    export AI_FOUNDRY
    export COMPOSEDASHBOARDBROWSERTOKEN
    export OPENWAAPIKEY
    export OPENWAENCRYPTIONKEY
    export OPENWAWEBHOOKSECRET
    export OPENWAPOSTGRESPASSWORD
    export OPENWA_REDIS_PASSWORD
    export WEBHOOK_BASE_URL
    export WEBHOOK_HOST_PORT="$webhook_host_port"
    export OPENWA_DASHBOARD_HOST_PORT="$openwa_dashboard_host_port"
    export CADDY_HTTP_HOST_PORT="$caddy_http_host_port"
    export MCPSERVER_IMAGE
    export WEBHOOK_IMAGE
    export OPENWA_IMAGE
    export OPENWA_DASHBOARD_IMAGE
    export MCPSERVER_PORT
    export WEBHOOK_PORT
    docker compose \
        -p "$compose_project" \
        -f "$compose_output_dir/docker-compose.yaml" \
        -f "$caddy_compose_path" \
        up -d
)

wait_for_caddy_route() {
    local host_header="$1"
    local path="$2"
    local attempts="${3:-30}"
    local url="http://127.0.0.1:${caddy_http_host_port}${path}"

    for _ in $(seq 1 "$attempts"); do
        if curl --silent --show-error --fail --output /dev/null -H "Host: ${host_header}" "$url"; then
            return 0
        fi

        sleep 1
    done

    die "Timed out waiting for Caddy route http://${host_header}${path}"
}

wait_for_caddy_route "$aspire_hostname" "/" 20
wait_for_caddy_route "$openwa_hostname" "/" 20
wait_for_caddy_route "$hostname" "/health" 45

save_current_demo_state "$vertical_id" "$compose_project" "$hostname" "$webhook_host_port" "$openwa_hostname" "$openwa_dashboard_host_port" "$aspire_hostname" "$caddy_http_host_port"

log "Demo deployed."
log "Aspire Dashboard URL: https://${aspire_hostname}/"
log "Aspire Dashboard token: ${COMPOSEDASHBOARDBROWSERTOKEN}"
log "OpenWA Dashboard URL: https://${openwa_hostname}/"
log "OpenWA Dashboard API key: ${OPENWAAPIKEY}"
log "Webhook public URL: ${WEBHOOK_BASE_URL}"
log "Compose project: ${compose_project}"
log "Cloudflare tunnel: managed by Docker Compose service cloudflared"
