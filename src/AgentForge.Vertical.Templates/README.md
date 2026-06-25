# AgentForge.Vertical.Templates

Template package for creating external AgentForge vertical plugins.

Install:

```bash
dotnet new install AgentForge.Vertical.Templates
```

Create a plugin:

```bash
dotnet new agentforge-vertical --name AgentForge.Verticals.School --verticalId school --displayName "School Assistant"
```

The generated project is a standalone Class Library. It references `AgentForge.Verticals.Abstractions`, `ModelContextProtocol`, and Microsoft configuration/DI abstractions, but does not reference AgentForge WebApi, McpHost, OpenWA, or other core implementation projects.

Publish the plugin and configure AgentForge with `VERTICAL_PLUGIN_PATH` or `VERTICAL_PLUGIN_ROOT` + `VERTICAL_ID`.
