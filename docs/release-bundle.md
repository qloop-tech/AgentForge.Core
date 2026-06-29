# AgentForge Core Release Bundle

This folder is a deployment bundle generated from `AgentForge.AppHost`.
It is meant to be copied to a VPS or local rehearsal machine with Docker installed.

## Files

| File | Purpose |
|---|---|
| `docker-compose.yaml` | Aspire-generated core platform stack. |
| `docker-compose.caddy.yaml` | Caddy ingress overlay for Aspire, OpenWA, and webhook hostnames. |
| `docker-compose.cloudflare.yaml` | Optional local Cloudflare Tunnel overlay for Mac mini rehearsal. |
| `.env.example` | Template for runtime values. `install.sh` writes `.env`. |
| `install.sh` | Installs or updates the stack. |
| `uninstall.sh` | Removes the stack. Use `--purge-all` to wipe AgentForge-owned data and images. |
| `plugins/` | Copy the published vertical plugin folder here, for example `plugins/travel/`. |

## Install

Copy a published plugin bundle into `plugins/<vertical-id>/`, then run:

```bash
./install.sh --mode vps
```

For local Mac mini rehearsal through Cloudflare Tunnel:

```bash
./install.sh --mode local-cloudflare
```

The installer prompts for the AI Foundry connection string, vertical id, plugin path,
and the three public hostnames:

- Aspire dashboard hostname
- OpenWA dashboard hostname
- webhook hostname

It generates and prints the Aspire dashboard token, OpenWA dashboard API key, OpenWA
encryption key, OpenWA webhook secret, OpenWA Postgres password, and OpenWA Redis password.

## Uninstall

Default uninstall preserves `.env`, plugins, images, and Docker volumes:

```bash
./uninstall.sh
```

Full wipe of AgentForge-owned containers, network, volumes, images, generated config,
and installer state:

```bash
./uninstall.sh --purge-all
```

Neither command uninstalls Docker or touches unrelated containers, images, volumes,
DNS records, or Cloudflare resources outside this release folder.
