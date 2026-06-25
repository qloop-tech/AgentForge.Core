# AgentForge.Verticals.Abstractions

Public contract package for AgentForge vertical plugins.

External verticals reference this package to implement:

- `IVerticalPlugin`
- `IVerticalDescriptor`
- `IVerticalMcpRegistrar`
- optional `IVerticalDeploymentValidator`
- optional `IScheduledActionHandler`

The package intentionally does not expose WebApi, McpHost, OpenWA, or outbound sender implementations. Plugins return business metadata, tools, resources, assets, and scheduled message intents; the AgentForge core platform owns provider behavior and sending.

See the AgentForge repository docs:

- `docs/vertical-plugin-system.md`
- `docs/plugin-development-getting-started.md`
