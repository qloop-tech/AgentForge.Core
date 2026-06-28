using System.Reflection;
using AgentForge.Verticals.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.TestVertical.Plugin;

public sealed class TestVerticalPlugin : IVerticalPlugin
{
    public IVerticalMcpRegistrar McpRegistrar { get; } = new TestVerticalMcpRegistrar();

    public void ConfigureConfiguration(IConfigurationManager configuration)
    {
    }

    public void RegisterCommonServices(IServiceCollection services)
    {
    }

    public void RegisterWebApiServices(IServiceCollection services)
    {
    }

    public IVerticalDescriptor CreateDescriptor(IServiceProvider serviceProvider)
        => new TestVerticalDescriptor();

    public string ResolveMcpServerName(IConfiguration configuration)
        => "test-mcp";
}

public sealed class TestVerticalMcpRegistrar : IVerticalMcpRegistrar
{
    public Assembly McpAssembly => typeof(TestVerticalMcpRegistrar).Assembly;

    public void RegisterServices(IServiceCollection services)
    {
    }
}

public sealed class TestVerticalDescriptor : IVerticalDescriptor
{
    public string Id => "test";

    public string DisplayName => "Test Vertical";

    public string AgentName => "Test Agent";

    public string AgentDescription => "Test vertical plugin used by core platform tests.";

    public string SystemPrompt => "You are a test agent.";

    public string McpServerName => "test-mcp";

    public string AssetRequestPathPrefix => "/test-assets";

    public string AssetRootPath => AppContext.BaseDirectory;

    public string PreviewTitle => "Test";

    public string PreviewDescription => "Test vertical plugin.";
}
