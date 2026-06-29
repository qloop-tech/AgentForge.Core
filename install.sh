#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

mode="macmini"
vertical_id="travel"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --mode)
            mode="${2:-}"
            shift 2
            ;;
        --vertical)
            vertical_id="${2:-}"
            shift 2
            ;;
        -h|--help)
            cat <<'EOF'
Usage:
  ./install.sh [--mode macmini] [--vertical travel]

Modes:
  macmini   Runs Docker Compose locally and exposes Caddy through Cloudflare Tunnel.
EOF
            exit 0
            ;;
        *)
            printf 'Unknown argument: %s\n' "$1" >&2
            exit 1
            ;;
    esac
done

if [[ "$mode" != "macmini" ]]; then
    printf "Unsupported mode '%s'. Currently implemented: macmini.\n" "$mode" >&2
    exit 1
fi

"${SCRIPT_DIR}/scripts/deploy-demo-vertical.sh" "$vertical_id"
