using AgentForge.Verticals.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Verticals.Hosting;

public sealed class VerticalPluginBootstrapState
{
    private readonly object _sync = new();
    private string? _descriptorError;

    private VerticalPluginBootstrapState(
        IVerticalPlugin? plugin,
        string? loadError,
        string expectedConfiguration)
    {
        Plugin = plugin;
        LoadError = loadError;
        ExpectedConfiguration = expectedConfiguration;
    }

    public IVerticalPlugin? Plugin { get; }

    public string? LoadError { get; }

    public string ExpectedConfiguration { get; }

    public bool IsLoaded => Plugin is not null;

    public static VerticalPluginBootstrapState Create(IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var pluginPath = configuration["VERTICAL_PLUGIN_PATH"];
        var pluginRoot = configuration["VERTICAL_PLUGIN_ROOT"];
        var verticalId = configuration["VERTICAL_ID"];
        var expectedConfiguration = GetExpectedConfiguration(pluginPath, pluginRoot, verticalId);

        try
        {
            var loader = VerticalPluginLoaderFactory.Create(pluginPath, pluginRoot, verticalId);
            var plugin = loader.Load();
            plugin.ConfigureConfiguration(configuration);
            return new VerticalPluginBootstrapState(plugin, loadError: null, expectedConfiguration);
        }
        catch (Exception ex)
        {
            return new VerticalPluginBootstrapState(plugin: null, Sanitize(ex.Message), expectedConfiguration);
        }
    }

    public IVerticalPlugin GetRequiredPlugin()
        => Plugin ?? throw new InvalidOperationException(GetUnhealthyMessage());

    public bool TryValidateReady(IServiceProvider serviceProvider, out string? error)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (Plugin is null)
        {
            error = GetUnhealthyMessage();
            return false;
        }

        lock (_sync)
        {
            if (_descriptorError is not null)
            {
                error = _descriptorError;
                return false;
            }

            try
            {
                _ = serviceProvider.GetRequiredService<IVerticalDescriptor>();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                _descriptorError = $"Vertical plugin descriptor could not be created. {Sanitize(ex.Message)}";
                error = _descriptorError;
                return false;
            }
        }
    }

    public string GetUnhealthyMessage()
        => LoadError is null
            ? "Vertical plugin is not loaded."
            : $"Vertical plugin failed to load. {LoadError} Expected configuration: {ExpectedConfiguration}";

    private static string GetExpectedConfiguration(string? pluginPath, string? pluginRoot, string? verticalId)
    {
        if (!string.IsNullOrWhiteSpace(pluginPath))
        {
            return $"VERTICAL_PLUGIN_PATH='{pluginPath}'";
        }

        if (!string.IsNullOrWhiteSpace(pluginRoot) || !string.IsNullOrWhiteSpace(verticalId))
        {
            return $"VERTICAL_PLUGIN_ROOT='{pluginRoot ?? "<missing>"}', VERTICAL_ID='{verticalId ?? "<missing>"}', resolved path '<root>/<vertical-id>'";
        }

        return "set VERTICAL_PLUGIN_PATH, or set VERTICAL_PLUGIN_ROOT and VERTICAL_ID";
    }

    private static string Sanitize(string message)
        => message.ReplaceLineEndings(" ").Trim();
}
