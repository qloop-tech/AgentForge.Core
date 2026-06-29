# Deployment Guide

AgentForge production deployment is release-bundle based. `AgentForge.AppHost` is the single source
of truth for the Docker Compose topology, generated Dockerfiles, image build graph, health checks,
volumes, OpenTelemetry, and Aspire dashboard wiring.

For architecture and plugin concepts, start with the [main README](../README.md).

## Release Pipeline

Publishing a GitHub Release for `qloop-tech/AgentForge.Core` runs `.github/workflows/release.yml`.

The workflow:

1. installs the .NET SDK and Aspire CLI
2. logs in to GHCR with `GITHUB_TOKEN`
3. runs `dotnet test AgentForge.slnx`
4. runs `aspire do push` from `src/AgentForge.AppHost/AgentForge.AppHost.csproj`
5. runs `aspire publish` to generate Compose artifacts
6. packages `agentforge-core-<release-tag>.zip`
7. uploads the zip and SHA-256 checksum to the GitHub Release

Core images are pushed to public GHCR packages and pinned to the exact GitHub Release tag:

```text
ghcr.io/qloop-tech/agentforge-core/mcpserver:vX.Y.Z
ghcr.io/qloop-tech/agentforge-core/webhook:vX.Y.Z
ghcr.io/qloop-tech/agentforge-core/openwa:vX.Y.Z
ghcr.io/qloop-tech/agentforge-core/openwa-dashboard:vX.Y.Z
```

Do not add separate hand-written production Dockerfiles. If the deployment shape changes, change
the AppHost and let Aspire regenerate the artifacts.

## VPS Install

Prerequisites on the target server:

- Docker with Docker Compose
- DNS records for three hostnames pointing to the VPS public IP:
  - Aspire dashboard
  - OpenWA dashboard
  - webhook
- the AgentForge release zip
- a published vertical plugin bundle copied to `plugins/<vertical-id>/`

Install:

```bash
unzip agentforge-core-vX.Y.Z.zip
cd agentforge-core-vX.Y.Z
mkdir -p plugins/travel
# copy the published Travel or customer vertical plugin files into plugins/<vertical-id>/
./install.sh --mode vps
```

The installer prompts for the Azure AI Foundry connection string, vertical id, plugin path, and the
three public hostnames. It generates and prints the Aspire dashboard token, OpenWA dashboard API key,
OpenWA encryption key, webhook secret, Postgres password, and Redis password.

VPS mode runs Caddy on ports `80` and `443` and routes:

```text
ASPIRE_HOSTNAME  -> compose-dashboard:18888
OPENWA_HOSTNAME  -> openwa-dashboard:5000
WEBHOOK_HOSTNAME -> webhook:8080
```

The published Aspire dashboard is intentionally the production dashboard: logs, traces, and metrics.
It is not the same as the local development resource-control dashboard.

## Local Cloudflare Rehearsal

Use the same release zip locally when rehearsing on a Mac mini. The only difference from VPS mode is
that Cloudflare Tunnel terminates public HTTPS and forwards traffic to Caddy.

```bash
unzip agentforge-core-vX.Y.Z.zip
cd agentforge-core-vX.Y.Z
mkdir -p plugins/travel
# copy plugin bundle into plugins/travel/
./install.sh --mode local-cloudflare
```

The installer asks for a Cloudflare Tunnel token in this mode. DNS/tunnel setup remains outside
AgentForge; the installer only starts the `cloudflared` service inside Compose.

## Uninstall

Default uninstall removes the AgentForge Compose containers and network but keeps `.env`, plugins,
images, and Docker volumes:

```bash
./uninstall.sh
```

Full wipe of AgentForge-owned state:

```bash
./uninstall.sh --purge-all
```

`--purge-all` removes AgentForge containers, network, volumes, release images, generated `.env`, and
installer state. It does not uninstall Docker, remove unrelated resources, delete DNS records, or
delete Cloudflare tunnel resources outside the release folder.

Use `--yes` only when you need non-interactive removal:

```bash
./uninstall.sh --purge-all --yes
```

## Manual Artifact Generation

Maintainers can generate a local release-style bundle without publishing a GitHub Release:

```bash
export IMAGE_TAG=v0.0.0-local
aspire publish --apphost src/AgentForge.AppHost/AgentForge.AppHost.csproj -o release/aspire-output
scripts/release/package-release.sh "$IMAGE_TAG" release/aspire-output release/dist
```

The resulting zip is under `release/dist/`.
