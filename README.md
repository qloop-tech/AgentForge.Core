# AgentForge — Multi-Vertical WhatsApp AI Platform

[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-purple.svg)](https://dotnet.microsoft.com/)
[![Aspire](https://img.shields.io/badge/Aspire-13.3-blueviolet.svg)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![OpenWA](https://img.shields.io/badge/OpenWA-self--hosted-green.svg)](https://www.open-wa.org/)
[![GitHub Stars](https://img.shields.io/github/stars/goldytech/whatsapp-ai-travel-agent?style=social)](https://github.com/goldytech/whatsapp-ai-travel-agent)

> ⭐ If this project saves you time or inspires your work, please **[give it a star](https://github.com/goldytech/whatsapp-ai-travel-agent)** — it helps others discover it and keeps the momentum going!

## Demo

![Demo](docs/demo.gif)

**AgentForge** is an open-source WhatsApp AI core platform for service businesses. It gives you the reusable host runtime for WhatsApp messaging, signed webhooks, Redis queueing, agent orchestration, MCP tool hosting, plugin loading, health checks, and Aspire-based deployment.

Industry behavior lives outside this core repository as vertical plugins. QLoop Technologies publishes the plugin contract and template packages separately, and Travel is now a standalone reference implementation:

- [AgentForge.Verticals.Abstractions](https://github.com/qloop-tech/AgentForge.Verticals.Abstractions)
- [AgentForge.Vertical.Templates](https://github.com/qloop-tech/AgentForge.Vertical.Templates)
- [AgentForge.Verticals.Travel](https://github.com/qloop-tech/AgentForge.Verticals.Travel)

The architecture is **plugin-first**: WebApi and McpHost require an explicit vertical plugin path and report unhealthy readiness when the plugin is missing or invalid.

---

## Documentation Map

| Start here | Use when you want to understand |
|---|---|
| [Architecture](docs/Architecture.md) | End-to-end message flow sequence diagram, roles, Redis queueing, inbound media guard, outbound media dispatch, and platform boundaries |
| [Vertical Plugin System](docs/vertical-plugin-system.md) | The class-level plugin contract and how to author another industry vertical |
| [Plugin Development Getting Started](docs/plugin-development-getting-started.md) | Tutorial for installing the external template package, creating, publishing, and loading a vertical plugin |
| [Deployment Guide](docs/deployment.md) | Aspire-generated Docker Compose, production-like local demos, and Cloudflare tunnel workflow |
| [OpenWA README](src/OpenWA/README.md) | The embedded WhatsApp API gateway and dashboard source used by AgentForge |
| [Repository Guidelines](docs/repository-guidelines.md) | Project structure, build/test commands, style rules, PR guidance, and security tips |

---

## Architecture

At runtime, a WhatsApp user message travels through OpenWA, the signed WebApi webhook, Redis dedupe and queueing, Aria's Azure AI Foundry-backed agent, the active vertical's MCP tools, and finally back through OpenWA as a WhatsApp reply.

The canonical architecture walkthrough is the **[Architecture](docs/Architecture.md)** guide. It contains the end-to-end sequence diagram, the role of each entity, the inbound media guard, Redis Streams queueing, outbound media dispatch, and platform boundaries.

For the class-level plugin loading flow and runtime contracts, see the **[Vertical Plugin System](docs/vertical-plugin-system.md)** deep dive.

---

## Technology Stack

| Layer | Technology | Purpose |
|---|---|---|
| **Runtime** | .NET 10 / C# 14 | All projects |
| **Orchestration** | [.NET Aspire 13.3](https://learn.microsoft.com/en-us/dotnet/aspire/) | Service discovery, health checks, OpenTelemetry, DevTunnel, secrets |
| **WhatsApp Gateway** | [OpenWA](https://www.open-wa.org/) (auto-selects a prebuilt amd64 image or source-build fallback by host architecture) | Self-hosted WhatsApp HTTP API + dashboard |
| **AI Agent Runtime** | [Microsoft Agents Framework 1.5](https://github.com/microsoft/agents) | `ChatClientAgent`, `AgentSession`, client-managed conversation history |
| **LLM** | [Azure AI Foundry](https://azure.microsoft.com/en-us/products/ai-foundry/) (GPT-5.4 mini) | Chat completions backing the active vertical agent |
| **AI Tool Protocol** | [Model Context Protocol 1.3](https://modelcontextprotocol.io/) | Structured HTTP-based tool server, auto-discovered by the agent |
| **Resilience** | [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/) (Polly v8) | Circuit breaker, timeouts — retries intentionally disabled to prevent duplicate messages |
| **Observability** | OpenTelemetry + Aspire Dashboard | Traces, structured logs, metrics across all services |
| **Public Tunnel** | [Azure DevTunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/) | Exposes the local webhook to the internet for OpenWA to call |

---

## Dynamic MCP Tool Wiring

`AgentForge.McpHost` is a **generic MCP host**. It does not permanently own travel logic anymore. Instead:

1. a vertical plugin exposes an `IVerticalPlugin`
2. that plugin exposes an `IVerticalMcpRegistrar`
3. the registrar points the host at the assembly containing the vertical's tools/resources
4. `AgentForge.McpHost` registers those tools/resources at runtime

This core repo does not ship a vertical toolset. Use [AgentForge.Verticals.Travel](https://github.com/qloop-tech/AgentForge.Verticals.Travel) as the reference vertical, or install [AgentForge.Vertical.Templates](https://github.com/qloop-tech/AgentForge.Vertical.Templates) to create a new vertical.

---

## Project Structure

```text
whatsapp-ai-travel-agent/
├── AgentForge.slnx
├── src/
│   ├── AgentForge.AppHost/          # .NET Aspire orchestration — defines all resources, dependencies, secrets
│   ├── AgentForge.ServiceDefaults/  # Shared defaults — OpenTelemetry, health checks, HTTP resilience, service discovery
│   ├── OpenWA/                      # First-party OpenWA API and Vite dashboard source, orchestrated by AppHost
│   ├── AgentForge.Verticals.Hosting/ # Shared loader boundary used by both hosts to resolve the active vertical
│   ├── AgentForge.McpHost/          # Generic MCP host — loads tools/resources from the active vertical plugin
│   ├── AgentForge.WebApi/           # AI gateway — receives webhooks, runs the active vertical agent, sends WhatsApp replies
│   │   ├── Endpoints/               #   WebhookEndpoint (/webhook)
│   │   ├── Services/                #   AgentChatService, VerticalAgentFactory, OpenWaApiClient, WebhookRegistrationService, McpClientProvider
│   │   ├── Queue/                   #   WhatsAppMessageQueue (Redis Streams background service)
│   │   └── Scheduling/              #   SchedulerService (generic scheduled action dispatcher)
├── tests/
│   ├── AgentForge.TestVertical.Plugin/ # Test-only plugin fixture for loader tests
│   └── AgentForge.WebApi.Tests/     # Webhook, OpenWA client, parser, dispatcher, guard, and loader tests
└── artifacts/                       # Reserved for build and plugin outputs
```

---

## How AgentForge Extends to Other Industries

This repository is now structured so the **host runtime stays generic** while each industry vertical owns its own domain behavior.

### Generic platform pieces

- `AgentForge.AppHost` — Aspire orchestration, secrets, OpenWA resources, DevTunnel, MCP Inspector, Compose publish flow
- `AgentForge.WebApi` — webhook handling, session management, queueing, agent execution, OpenWA sending
- `AgentForge.McpHost` — generic MCP host that loads tools/resources from the active vertical
- `AgentForge.Verticals.Abstractions` — external NuGet contract package for `IVerticalPlugin`, `IVerticalDescriptor`, `IVerticalMcpRegistrar`, and `IScheduledActionHandler`
- `AgentForge.Verticals.Hosting` — `AssemblyLoadContext`-based plugin loading, bootstrap state, and plugin health checks

### Vertical-owned pieces

Each vertical can own:

- runtime config packs, prompts, and branding
- MCP tools and MCP resources
- domain services/models
- industry seed data
- scheduled action behavior
- WebApi service registrations specific to that industry

### Current plugin contract

At a high level, a vertical plugs in by implementing:

- `IVerticalDescriptor` — runtime-composed display name, agent metadata, system prompt, preview defaults, asset prefix
- `IVerticalMcpRegistrar` — which assembly contains the vertical's MCP tools/resources and which services to register for MCP
- `IVerticalPlugin` — configuration sources, common services, runtime descriptor creation, MCP registrar, and WebApi service registration
- `IScheduledActionHandler` — industry-specific reminder/follow-up behavior

### Runtime selection model

AgentForge requires an explicit vertical plugin configuration:

1. **Direct path** — set `VERTICAL_PLUGIN_PATH` to an external plugin folder or DLL
2. **Plugin root + id** — set `VERTICAL_PLUGIN_ROOT` and `VERTICAL_ID` so the loader resolves `<root>/<id>`

If no plugin is configured or the plugin cannot load, `AgentForge.WebApi` and `AgentForge.McpHost` stay running but report `Unhealthy` on `/health` so Aspire Dashboard shows the bootstrap problem clearly.

### Creating a new industry vertical

To add a new industry such as school, clinic, or salon:

1. install `AgentForge.Vertical.Templates`
2. create a new external class library with `dotnet new agentforge-vertical`
3. reference `AgentForge.Verticals.Abstractions` and implement the descriptor, MCP registrar, plugin entry point, tools, resources, data, and optional scheduled action handler
4. publish the plugin bundle to `artifacts/plugins/<vertical-id>/`
5. point `VERTICAL_PLUGIN_PATH` or `VERTICAL_PLUGIN_ROOT` + `VERTICAL_ID` at it

The result is the same generic WhatsApp host runtime with a different business-specific capability set loaded at runtime.

---

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | `dotnet --version` to verify |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | Required to run the OpenWA API/dashboard plus provider-side Postgres/Redis |
| [Aspire CLI](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/aspire-sdk-tooling) | 13.3+ | `dotnet tool install -g aspire` |
| [Azure DevTunnel CLI](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started) | Latest | `devtunnel user login` before running |
| [Azure AI Foundry](https://azure.microsoft.com/en-us/products/ai-foundry/) | — | Deployed GPT-5.4 mini (or compatible model) |
| OpenWA API Key | — | Any string — you set this yourself in secrets |

---

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/goldytech/whatsapp-ai-travel-agent.git
cd whatsapp-ai-travel-agent
```

### 2. Configure secrets

All sensitive values are stored in .NET user secrets (never committed to source control).

```bash
# Set OpenWA credentials (choose your own values)
cd src/AgentForge.AppHost
dotnet user-secrets set "Parameters:openWaApiKey"          "your-api-key"
dotnet user-secrets set "Parameters:openWaEncryptionKey"   "generate-a-long-random-secret"
dotnet user-secrets set "Parameters:openWaWebhookSecret"   "generate-a-second-long-random-secret"
dotnet user-secrets set "Parameters:openWaPostgresPassword" "set-a-strong-database-password"

# Set Azure AI Foundry connection string
# Format: Endpoint=https://<resource>.services.ai.azure.com/models;Key=<key>
dotnet user-secrets set "ConnectionStrings:ai-foundry" "Endpoint=https://...;Key=...;"
```

> **Tip:** `openWaApiKey` protects the OpenWA REST API. `openWaWebhookSecret` is a separate shared secret used for OpenWA's HMAC-signed webhook delivery to `/webhook`. Keep them different.

### 3. Publish and configure a vertical plugin

The core platform does not silently fall back to any vertical. For the Travel reference implementation, clone and publish the external Travel plugin:

```bash
cd ../..
git clone https://github.com/qloop-tech/AgentForge.Verticals.Travel.git ../AgentForge.Verticals.Travel
dotnet publish ../AgentForge.Verticals.Travel/src/AgentForge.Verticals.Travel/AgentForge.Verticals.Travel.csproj -c Release -o artifacts/plugins/travel
cd src/AgentForge.AppHost
dotnet user-secrets set "Parameters:vertical-plugin-path" "$(pwd)/../../artifacts/plugins/travel"
```

For a new vertical, install `AgentForge.Vertical.Templates`, generate a plugin, and publish that plugin folder instead.

### 4. Log in to DevTunnel

```bash
devtunnel user login
```

### 5. Start the application

```bash
aspire start
```

Aspire will:
- Pull and start the OpenWA API/dashboard plus provider-side Postgres/Redis containers
- Start `AgentForge.McpHost` and `AgentForge.WebApi`
- Create a DevTunnel and register the webhook URL with OpenWA automatically

Open the Aspire Dashboard link printed in the terminal to monitor all services.

### 6. Connect WhatsApp (scan QR)

Follow the **OpenWA Dashboard Configuration** section below to link your WhatsApp account.

---

## OpenWA Dashboard Configuration

OpenWA exposes a management dashboard to connect your WhatsApp account.

### Access the dashboard

1. In the Aspire Dashboard, find the **openwa-dashboard** container resource
2. Click the **OpenWA Dashboard** link (opens `http://localhost:<port>/`)
3. If the UI prompts for API access, enter the `openWaApiKey` you configured in secrets

### Create and start a session

1. Open or create the `default` session (the code uses this session name)
2. Start or resume the session if it is not already active

### Link your WhatsApp account

1. Once status reaches `SCAN_QR`, click the QR icon
2. On your phone: **WhatsApp → Linked Devices → Link a Device**
3. Scan the QR code
4. Status changes to `CONNECTED` — your bot is live ✅

### Session persistence

The API container uses a **Persistent lifetime** in Aspire, meaning it survives `aspire stop` / `aspire start` cycles. The OpenWA session data is stored in a Docker volume (`openwa-data`). On restarts, `WebhookRegistrationService` automatically re-registers the webhook and reconciles the `default` session state.

> **Troubleshooting:** If the session shows `DISCONNECTED` or `STOPPED`, the service attempts a restart on the next `aspire start`. You can also trigger it manually via the OpenWA dashboard.

### Webhook authenticity

OpenWA webhooks are configured with an HMAC secret and `AgentForge.WebApi` verifies every `/webhook` request against the raw request body before any JSON is parsed.

- OpenWA sends `X-OpenWA-Signature`
- The app currently expects the standard `sha256=<hex>` signature format
- Invalid or unsigned webhook requests are rejected before they reach the message queue

The manual `POST /admin/register-webhook` endpoint is available in **Development** only.

---

## MCP Inspector

The [MCP Inspector](https://github.com/modelcontextprotocol/inspector) is a browser-based developer tool for interactively exploring and testing the tools and resources exposed by `AgentForge.McpHost`. It is included automatically in the Aspire application when running locally — no separate install required.

### Open the inspector

1. Run `aspire start` and open the **Aspire Dashboard** link printed in the terminal
2. Find the **mcp-inspector** resource and click its **Client** endpoint link
3. The inspector opens in your browser at `http://localhost:6274`

### Connect to the MCP server

1. In the **Transport Type** dropdown, select **Streamable HTTP**
2. The **Server URL** field will be pre-filled with the local `AgentForge.McpHost` endpoint (e.g. `http://localhost:<port>/mcp`)
3. Click **Connect**, then click **Initialize**
4. You can now browse all **18 tools** and **3 resources** — execute them with custom arguments and inspect the responses in real time

### Node.js v24 compatibility note

The default inspector version (`0.17.2`) crashes on Node.js v24+ with `ERR_INVALID_STATE: Controller is already closed`. The AppHost pins the inspector to `0.17.5` which includes the fix:

```csharp
// AgentForge.AppHost/AppHost.cs
builder.AddMcpInspector("mcp-inspector", options =>
{
    options.InspectorVersion = "0.17.5";
}).WithMcpServer(mcpServer);
```

If you upgrade the `CommunityToolkit.Aspire.Hosting.McpInspector` package in the future, verify the bundled default version is `0.17.5` or later before removing the explicit pin.

---

## Configuration Reference

Secrets stay in AppHost user secrets. Customer-facing branding, prompt text, and business-profile settings can be layered from a mounted config folder without recompiling the travel plugin.

Aspire parameters are used for **promptable runtime inputs**. Graph-shaping AppHost values stay as ordinary configuration so the resource graph can be built deterministically before the dashboard starts.

| Secret / Env Var | Where set | Description |
|---|---|---|
| `Parameters:openWaApiKey` | `AgentForge.AppHost` user secrets | API key protecting the OpenWA REST endpoints |
| `Parameters:openWaEncryptionKey` | `AgentForge.AppHost` user secrets | Provider-side encryption key for OpenWA runtime data |
| `Parameters:openWaWebhookSecret` | `AgentForge.AppHost` user secrets | Shared secret used by OpenWA to HMAC-sign webhook POST bodies |
| `Parameters:openWaPostgresPassword` | `AgentForge.AppHost` user secrets | Password for the Aspire-managed PostgreSQL instance used by OpenWA |
| `ConnectionStrings:ai-foundry` | `AgentForge.AppHost` user secrets | Azure AI Foundry connection string (`Endpoint=...;Key=...`) |
| `WEBHOOK_BASE_URL` | Optional env var on `AgentForge.WebApi` | Override the webhook URL if not using DevTunnel |
| `OPENWA_DASHBOARD_HOST_PORT` | Optional env var on published Compose deployments | Host port exposing the OpenWA dashboard (`2886` by default) |
| `VERTICAL_ID` | Optional env var on `AgentForge.AppHost` | Active vertical ID for Compose publishing and runtime selection (`travel` by default) |
| `VERTICAL_PLUGIN_ROOT` | Optional env var on `AgentForge.AppHost` | Container-side root path mounted into `AgentForge.WebApi` and `AgentForge.McpHost` during Compose publish (`/app/plugins` by default) |
| `VERTICAL_PLUGIN_SOURCE_PATH` | Optional env var on `AgentForge.AppHost` | Host-side plugin folder to bind-mount during Compose publish (defaults to `../../artifacts/plugins/{VERTICAL_ID}` relative to `src/AgentForge.AppHost/`) |
| `CUSTOMER_CONFIG_SOURCE_PATH` | Optional env var on `AgentForge.AppHost` | Host-side customer config folder to bind-mount during Compose publish; when set, the AppHost also passes `CUSTOMER_CONFIG_PATH` into both hosts |
| `CUSTOMER_CONFIG_PATH` | Optional env var on `AgentForge.WebApi` / `AgentForge.McpHost` | Path to a customer config folder containing `customer-profile.json` and optionally `prompt.md`; when unset, the active plugin can use its bundled defaults |
| `VERTICAL_PLUGIN_PATH` | Env var on `AgentForge.WebApi` / `AgentForge.McpHost` | Path to an external published vertical plugin folder or DLL |

### Optional dashboard local overrides

When you run `aspire start` locally, the AppHost exposes these as Aspire parameters:

- `vertical-plugin-path` — optional local override for an external plugin folder or DLL
- `customer-config-path` — optional local override for a customer config folder

They default to blank, so local startup no longer blocks on unresolved parameters before the dashboard opens. Blank plugin configuration means:

- no plugin loaded; `/health` reports `Unhealthy` until a valid plugin is configured
- the active plugin uses bundled customer config, when it provides bundled defaults

If you want to preconfigure these overrides without using the dashboard, the canonical Aspire parameter keys are `Parameters:vertical-plugin-path` and `Parameters:customer-config-path`. For env-style configuration, the AppHost also accepts these shell-friendly aliases:

- `Parameters__vertical_plugin_path`
- `Parameters__customer_config_path`

Legacy `VERTICAL_PLUGIN_PATH` and `CUSTOMER_CONFIG_PATH` environment variables are still accepted as compatibility fallbacks for local runs.

The dashboard parameters are therefore an **optional override UX**. A valid plugin still must be configured either with `vertical-plugin-path` / `VERTICAL_PLUGIN_PATH` or with `VERTICAL_PLUGIN_ROOT` + `VERTICAL_ID`. Values like `VERTICAL_ID`, `VERTICAL_PLUGIN_SOURCE_PATH`, and `CUSTOMER_CONFIG_SOURCE_PATH` still shape the AppHost graph or publish output, so they remain standard AppHost configuration rather than dashboard-entered parameters.

### Customer config pack layout

Vertical plugins may provide bundled defaults and may also support customer configuration overrides. To onboard a customer without recompiling, mount a folder with this shape and point `CUSTOMER_CONFIG_PATH` at it:

```text
customer-config/
├── customer-profile.json
└── prompt.md   # optional override; falls back to the bundled prompt if omitted
```

The exact schema is owned by the active vertical plugin.

---

---

## Contributing

Contributions are welcome! Please follow these steps:

1. **Open an issue** first to discuss the change (bug, feature, or improvement)
2. **Fork** the repository and create a branch:
   ```
   git checkout -b feat/<issue-number>-short-description
   ```
3. Make your changes following the existing code style:
   - C# 14 features where appropriate (`field`-backed properties, extension members, `System.Threading.Lock`, collection expressions, etc.)
   - Primary constructors for services
   - `ConfigureAwait(false)` on all `await` calls in library/service code
   - No `#pragma warning disable` — fix the root cause instead
4. **Build and verify** with `dotnet build` (zero warnings expected)
5. Open a **Pull Request** — reference the issue in the PR description

### Code style highlights

- Services are registered as `Singleton` or `Scoped` — never `Transient` for stateful classes
- HTTP clients use `IHttpClientFactory` (typed or named) — no `new HttpClient()`
- Background work uses Redis Streams, channels, or `IHostedService` — no `_ = Task.Run(...)`
- Retries are intentionally disabled on `OpenWaApiClient` — retrying a send request can produce duplicate WhatsApp messages

---

---

## Support This Project

If **AgentForge** saved you time, sparked an idea, or helped you learn something new — a ⭐ on GitHub means a lot to an open-source builder and helps others discover the project.

**[⭐ Star on GitHub](https://github.com/goldytech/whatsapp-ai-travel-agent)**

---

## License

This project is licensed under the [MIT License](LICENSE).

© 2026 Muhammad Afzal Qureshi
