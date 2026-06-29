# Copilot Instructions — AgentForge Core

AgentForge Core is the open-source WhatsApp AI host platform. Vertical business logic lives in
external plugin repositories and is loaded at runtime.

## Build, Test, Run

```bash
dotnet build AgentForge.slnx
dotnet test AgentForge.slnx
aspire start --apphost src/AgentForge.AppHost/AgentForge.AppHost.csproj
aspire publish --apphost src/AgentForge.AppHost/AgentForge.AppHost.csproj -o artifacts/aspire-output
```

Use `rg` for searches. Do not add hand-written production Dockerfiles; `AgentForge.AppHost` owns the
deployment topology and generated Dockerfiles.

## Architecture

| Project | Role |
|---|---|
| `AgentForge.AppHost` | Aspire orchestration, registry wiring, Compose publish, Caddy-compatible deployment metadata |
| `AgentForge.WebApi` | Signed WhatsApp webhook, Redis queue, agent execution, OpenWA outbound sending |
| `AgentForge.McpHost` | Generic MCP host that loads tools/resources from the active vertical plugin |
| `AgentForge.Verticals.Hosting` | Runtime plugin bootstrap, loader, and `vertical-plugin` health check |
| `AgentForge.ServiceDefaults` | OpenTelemetry, health endpoints, service discovery, HTTP resilience |
| `OpenWA` | Embedded WhatsApp API and dashboard source hosted by AppHost |

## Plugin Boundary

- Core does not silently fall back to Travel.
- A valid plugin must be configured through `VERTICAL_PLUGIN_PATH` or `VERTICAL_PLUGIN_ROOT` + `VERTICAL_ID`.
- Missing/invalid plugins make WebApi and McpHost readiness unhealthy.
- Travel is a separate reference repo: `qloop-tech/AgentForge.Verticals.Travel`.

## Deployment Rules

- Release images are built and pushed by Aspire CLI from the AppHost, not custom Docker commands.
- Release image tags must match the GitHub Release tag, including the `v` prefix.
- Production deployments use the release zip plus `.env`; they do not use AppHost user-secrets.
- `install.sh` is the supported VPS/local-Cloudflare installer.
- `uninstall.sh` is the supported removal path; `--purge-all` is the only destructive wipe option.
- Local Mac mini rehearsal should use the release-bundle `.env` flow so it mirrors VPS deployment. Cloudflare is the only expected difference.

## Coding Conventions

- Prefer existing project patterns and keep changes scoped.
- Use primary constructors where already used.
- Use `ConfigureAwait(false)` in services/library code.
- Keep retries disabled for OpenWA send paths to avoid duplicate WhatsApp messages.
- Add tests for loader, webhook, deployment-script, or health behavior when changing those surfaces.
