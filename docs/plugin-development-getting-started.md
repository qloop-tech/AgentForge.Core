# Plugin Development Getting Started

This tutorial creates an external AgentForge vertical plugin as a separate Class Library project. The plugin can live in another repository, be built independently, and be copied into the plugin folder that AgentForge loads at runtime.

## 1. Install the Template Package

```bash
dotnet new install AgentForge.Vertical.Templates
```

Before the package is published to NuGet.org, maintainers can install from a locally packed `.nupkg` created by the external template repo:

```bash
dotnet pack ../AgentForge.Vertical.Templates/src/AgentForge.Vertical.Templates/AgentForge.Vertical.Templates.csproj
dotnet new install <path-to-packed-AgentForge.Vertical.Templates.0.1.0.nupkg>
```

## 2. Create a New Vertical

Create the plugin outside the core AgentForge solution:

```bash
mkdir SchoolAssistant
cd SchoolAssistant
dotnet new agentforge-vertical --name AgentForge.Verticals.School --verticalId school --displayName "School Assistant"
```

The generated project is a normal .NET Class Library. It references:

- `AgentForge.Verticals.Abstractions`
- `ModelContextProtocol`
- Microsoft configuration/DI abstractions

It does not reference `AgentForge.WebApi`, `AgentForge.McpHost`, OpenWA, or other core implementation projects.

## 3. Customize the Plugin

Update these generated areas:

| Area | What to customize |
|---|---|
| `*VerticalPlugin.cs` | Plugin entry point, configuration files, common DI registrations, optional deployment validation. |
| `*VerticalDescriptor.cs` | Agent name, description, prompt, MCP server name, asset paths, preview metadata. |
| `*McpRegistrar.cs` | Services used by MCP tools/resources. |
| `Tools/` | MCP tools the AI agent can call. |
| `Resources/` | MCP resources exposed by the plugin. |
| `Configuration/prompt.md` | System prompt for the vertical. |
| `Configuration/customer-profile.json` | Customer or vertical profile defaults. |
| `Data/` | Seed data such as courses, services, products, packages, policies, or FAQs. |
| `Assets/` | Approved outbound media assets. |

## 4. Build and Publish the Plugin Bundle

```bash
dotnet restore
dotnet build
dotnet publish -c Release -o ../artifacts/plugins/school
```

The output folder should contain the plugin DLL, dependencies, config, data, and assets:

```text
artifacts/plugins/school/
├── AgentForge.Verticals.School.dll
├── AgentForge.Verticals.School.deps.json
├── Configuration/
├── Data/
└── Assets/
```

## 5. Configure AgentForge to Load It

Use either a direct path:

```bash
export VERTICAL_PLUGIN_PATH=/absolute/path/to/artifacts/plugins/school
```

Or use plugin root plus vertical id:

```bash
export VERTICAL_PLUGIN_ROOT=/absolute/path/to/artifacts/plugins
export VERTICAL_ID=school
```

For published Compose deployments, copy or mount the plugin folder into the configured plugin root.

## 6. Verify in Aspire Dashboard

Start AgentForge:

```bash
aspire start
```

Check the Aspire Dashboard:

- `webhook` should be healthy
- `mcpserver` should be healthy
- if the plugin path is missing or invalid, both resources should show unhealthy
- `/health` should include the `vertical-plugin` check
- `/alive` should remain healthy when only plugin readiness fails

## 7. Development Rules

- Keep business behavior in the plugin.
- Keep WhatsApp/OpenWA sending in the core platform.
- Do not reference WebApi or McpHost from plugin projects.
- Do not guess arbitrary outbound media URLs; use approved plugin assets and marker formats.
- Publish the whole plugin folder, not only the main DLL.
