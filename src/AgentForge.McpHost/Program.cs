using AgentForge.Verticals.Abstractions;
using AgentForge.Verticals.Hosting;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);
var verticalPluginBootstrapState = VerticalPluginBootstrapState.Create(builder.Configuration);

builder.AddServiceDefaults();
builder.Services.AddVerticalPluginBootstrap(verticalPluginBootstrapState);
builder.Services.AddSingleton<IVerticalMcpRegistrar>(sp => sp.GetRequiredService<IVerticalPlugin>().McpRegistrar);
verticalPluginBootstrapState.Plugin?.RegisterCommonServices(builder.Services);
verticalPluginBootstrapState.Plugin?.McpRegistrar.RegisterServices(builder.Services);

if (verticalPluginBootstrapState.Plugin is { } verticalPlugin)
{
    // MCP Server with all tools and resources
    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation { Name = verticalPlugin.ResolveMcpServerName(builder.Configuration), Version = "1.0.0" };
        })
        .WithToolsFromAssembly(verticalPlugin.McpRegistrar.McpAssembly)
        .WithResourcesFromAssembly(verticalPlugin.McpRegistrar.McpAssembly)
        .WithHttpTransport();
}

var app = builder.Build();

app.MapDefaultEndpoints();

if (verticalPluginBootstrapState.IsLoaded)
{
    app.MapMcp("/mcp");
}
else
{
    app.MapMethods("/mcp", ["GET", "POST"], (VerticalPluginBootstrapState state) =>
    {
        return Results.Problem(
            title: "Vertical plugin unavailable",
            detail: state.GetUnhealthyMessage(),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    });
}

app.Run();
