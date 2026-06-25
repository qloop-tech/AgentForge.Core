using System.Diagnostics.CodeAnalysis;
using AgentForge.Verticals.Hosting;
using AgentForge.Verticals.Travel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentForge.WebApi.Tests;

[ExcludeFromCodeCoverage]
public sealed class VerticalPluginBootstrapStateTests
{
    [Fact]
    public void Create_without_plugin_configuration_is_unhealthy()
    {
        var state = VerticalPluginBootstrapState.Create(new ConfigurationManager());

        Assert.False(state.IsLoaded);
        Assert.Contains("No vertical plugin was configured", state.GetUnhealthyMessage());
    }

    [Fact]
    public void Create_with_missing_plugin_folder_is_unhealthy()
    {
        var configuration = new ConfigurationManager();
        configuration["VERTICAL_PLUGIN_PATH"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var state = VerticalPluginBootstrapState.Create(configuration);

        Assert.False(state.IsLoaded);
        Assert.Contains("does not exist", state.GetUnhealthyMessage());
        Assert.Contains("VERTICAL_PLUGIN_PATH", state.GetUnhealthyMessage());
    }

    [Fact]
    public void Create_with_empty_plugin_folder_is_unhealthy()
    {
        var pluginFolder = CreateTempDirectory();
        var configuration = new ConfigurationManager();
        configuration["VERTICAL_PLUGIN_PATH"] = pluginFolder;

        var state = VerticalPluginBootstrapState.Create(configuration);

        Assert.False(state.IsLoaded);
        Assert.Contains("No vertical plugin assembly", state.GetUnhealthyMessage());
    }

    [Fact]
    public void Create_with_multiple_plugin_assemblies_is_unhealthy()
    {
        var pluginFolder = CreateTempDirectory();
        File.WriteAllText(Path.Combine(pluginFolder, "AgentForge.Verticals.One.dll"), string.Empty);
        File.WriteAllText(Path.Combine(pluginFolder, "AgentForge.Verticals.Two.dll"), string.Empty);
        var configuration = new ConfigurationManager();
        configuration["VERTICAL_PLUGIN_PATH"] = pluginFolder;

        var state = VerticalPluginBootstrapState.Create(configuration);

        Assert.False(state.IsLoaded);
        Assert.Contains("Multiple vertical plugin assemblies", state.GetUnhealthyMessage());
    }

    [Fact]
    public void Create_with_valid_direct_plugin_path_loads_plugin()
    {
        var configuration = new ConfigurationManager();
        configuration["VERTICAL_PLUGIN_PATH"] = typeof(TravelVerticalPlugin).Assembly.Location;

        var state = VerticalPluginBootstrapState.Create(configuration);

        Assert.True(state.IsLoaded);
        Assert.Equal(typeof(TravelVerticalPlugin).FullName, state.Plugin?.GetType().FullName);
    }

    [Fact]
    public void TryValidateReady_returns_true_when_descriptor_can_be_created()
    {
        var configuration = new ConfigurationManager();
        configuration["VERTICAL_PLUGIN_PATH"] = typeof(TravelVerticalPlugin).Assembly.Location;
        var state = VerticalPluginBootstrapState.Create(configuration);
        using var provider = CreateProvider(state, configuration);

        var ready = state.TryValidateReady(provider, out var error);

        Assert.True(ready);
        Assert.Null(error);
    }

    [Fact]
    public async Task HealthCheck_reports_unhealthy_when_plugin_is_missing()
    {
        var state = VerticalPluginBootstrapState.Create(new ConfigurationManager());
        using var provider = CreateProvider(state);
        var healthCheck = new VerticalPluginHealthCheck(state, provider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("No vertical plugin was configured", result.Description);
    }

    [Fact]
    public async Task HealthCheck_reports_healthy_when_plugin_is_ready()
    {
        var configuration = new ConfigurationManager();
        configuration["VERTICAL_PLUGIN_PATH"] = typeof(TravelVerticalPlugin).Assembly.Location;
        var state = VerticalPluginBootstrapState.Create(configuration);
        using var provider = CreateProvider(state, configuration);
        var healthCheck = new VerticalPluginHealthCheck(state, provider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static ServiceProvider CreateProvider(VerticalPluginBootstrapState state)
        => CreateProvider(state, new ConfigurationManager());

    private static ServiceProvider CreateProvider(VerticalPluginBootstrapState state, IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddVerticalPluginBootstrap(state);
        state.Plugin?.RegisterCommonServices(services);
        return services.BuildServiceProvider();
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "agentforge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
