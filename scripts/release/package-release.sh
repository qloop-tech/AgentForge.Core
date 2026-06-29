#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/../.." && pwd)"

usage() {
    cat <<'EOF'
Usage:
  scripts/release/package-release.sh <release-version> <aspire-output-dir> <output-dir>

Example:
  scripts/release/package-release.sh v0.2.0 release/aspire-output release/dist
EOF
}

[[ "${1:-}" != "-h" && "${1:-}" != "--help" ]] || {
    usage
    exit 0
}

RELEASE_VERSION="${1:-}"
ASPIRE_OUTPUT_DIR="${2:-}"
OUTPUT_DIR="${3:-}"

[[ -n "$RELEASE_VERSION" ]] || {
    usage >&2
    exit 1
}
[[ -d "$ASPIRE_OUTPUT_DIR" ]] || {
    printf 'Aspire output directory not found: %s\n' "$ASPIRE_OUTPUT_DIR" >&2
    exit 1
}
[[ -n "$OUTPUT_DIR" ]] || {
    usage >&2
    exit 1
}

BUNDLE_DIR="${OUTPUT_DIR}/agentforge-core-${RELEASE_VERSION}"
ZIP_PATH="${OUTPUT_DIR}/agentforge-core-${RELEASE_VERSION}.zip"

rm -rf "$BUNDLE_DIR" "$ZIP_PATH"
mkdir -p "$BUNDLE_DIR/plugins" "$OUTPUT_DIR"

cp "$ASPIRE_OUTPUT_DIR/docker-compose.yaml" "$BUNDLE_DIR/docker-compose.yaml"
cp "$ASPIRE_OUTPUT_DIR/.env" "$BUNDLE_DIR/.env.example"

find "$ASPIRE_OUTPUT_DIR" -maxdepth 1 -name '*.Dockerfile' -exec cp {} "$BUNDLE_DIR/" \;

cp "$REPO_ROOT/install.sh" "$BUNDLE_DIR/install.sh"
cp "$REPO_ROOT/uninstall.sh" "$BUNDLE_DIR/uninstall.sh"
cp "$REPO_ROOT/Caddyfile.template" "$BUNDLE_DIR/Caddyfile.template"
cp "$REPO_ROOT/docker-compose.caddy.yaml" "$BUNDLE_DIR/docker-compose.caddy.yaml"
cp "$REPO_ROOT/docker-compose.cloudflare.yaml" "$BUNDLE_DIR/docker-compose.cloudflare.yaml"
cp "$REPO_ROOT/docs/release-bundle.md" "$BUNDLE_DIR/README.md"
touch "$BUNDLE_DIR/plugins/.gitkeep"
printf '%s\n' "$RELEASE_VERSION" >"$BUNDLE_DIR/VERSION"

python3 - "$BUNDLE_DIR/.env.example" "$RELEASE_VERSION" <<'PY'
from pathlib import Path
import sys

env_path = Path(sys.argv[1])
version = sys.argv[2]
values = {
    "RELEASE_VERSION": version,
    "IMAGE_TAG": version,
    "MCPSERVER_IMAGE": f"ghcr.io/qloop-tech/agentforge-core/mcpserver:{version}",
    "WEBHOOK_IMAGE": f"ghcr.io/qloop-tech/agentforge-core/webhook:{version}",
    "OPENWA_IMAGE": f"ghcr.io/qloop-tech/agentforge-core/openwa:{version}",
    "OPENWA_DASHBOARD_IMAGE": f"ghcr.io/qloop-tech/agentforge-core/openwa-dashboard:{version}",
    "MCPSERVER_PORT": "8081",
    "WEBHOOK_PORT": "8080",
    "WEBHOOK_HOST_PORT": "8080",
    "OPENWA_DASHBOARD_HOST_PORT": "2886",
    "CADDY_HTTP_HOST_PORT": "80",
    "CADDY_HTTPS_HOST_PORT": "443",
    "ASPIRE_HOSTNAME": "aspire.example.com",
    "OPENWA_HOSTNAME": "openwa.example.com",
    "WEBHOOK_HOSTNAME": "webhook.example.com",
    "AGENTFORGE_CADDYFILE_PATH": "./.agentforge/Caddyfile",
    "CLOUDFLARE_TUNNEL_TOKEN": "set-by-install-sh-in-local-cloudflare-mode",
}

lines = env_path.read_text().splitlines()
seen = set()
out = []
for line in lines:
    if "=" in line and not line.lstrip().startswith("#"):
        key = line.split("=", 1)[0]
        if key in values:
            out.append(f"{key}={values[key]}")
            seen.add(key)
            continue
    out.append(line)

for key, value in values.items():
    if key not in seen:
        out.append(f"{key}={value}")

env_path.write_text("\n".join(out) + "\n")
PY

if grep -R "/Users/" "$BUNDLE_DIR" >/dev/null 2>&1; then
    printf 'Release bundle contains a local /Users path.\n' >&2
    grep -R "/Users/" "$BUNDLE_DIR" >&2 || true
    exit 1
fi

chmod +x "$BUNDLE_DIR/install.sh" "$BUNDLE_DIR/uninstall.sh"

(
    cd "$OUTPUT_DIR"
    zip -qr "$(basename "$ZIP_PATH")" "$(basename "$BUNDLE_DIR")"
    shasum -a 256 "$(basename "$ZIP_PATH")" >"$(basename "$ZIP_PATH").sha256"
)

printf '%s\n' "$ZIP_PATH"
