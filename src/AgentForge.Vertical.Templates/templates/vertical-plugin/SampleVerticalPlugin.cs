using AgentForge.Verticals.Abstractions;
using AgentForge.Vertical.Template.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Vertical.Template;

public sealed class SampleVerticalPlugin : IVerticalPlugin, IVerticalDeploymentValidator
{
    public IVerticalMcpRegistrar McpRegistrar { get; } = new SampleMcpRegistrar();

    public void ConfigureConfiguration(IConfigurationManager configuration)
    {
        var configPath = Path.Combine(SamplePluginPaths.Configuration, "customer-profile.json");
        if (File.Exists(configPath))
        {
            configuration.AddJsonFile(configPath, optional: false, reloadOnChange: false);
        }
    }

    public void RegisterCommonServices(IServiceCollection services)
    {
        services.AddSingleton<SampleCatalogService>();
    }

    public void RegisterWebApiServices(IServiceCollection services)
    {
        // Register WebApi-only vertical services here if needed.
        // Do not register message senders; outbound transport belongs to AgentForge core.
    }

    public IVerticalDescriptor CreateDescriptor(IServiceProvider serviceProvider)
    {
        var promptPath = Path.Combine(SamplePluginPaths.Configuration, "prompt.md");
        var prompt = File.Exists(promptPath)
            ? File.ReadAllText(promptPath)
            : "You are a helpful Sample Assistant.";

        return new SampleVerticalDescriptor(prompt);
    }

    public string ResolveMcpServerName(IConfiguration configuration)
        => "sample-mcp";

    public void ValidateDeployment()
    {
        EnsureDirectory("Configuration");
        EnsureDirectory("Data");
    }

    private static void EnsureDirectory(string directoryName)
    {
        var path = Path.Combine(SamplePluginPaths.Root, directoryName);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Required vertical directory '{path}' was not found.");
        }
    }
}
