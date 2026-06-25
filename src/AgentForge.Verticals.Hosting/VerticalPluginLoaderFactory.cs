using AgentForge.Verticals.Abstractions;

namespace AgentForge.Verticals.Hosting;

public static class VerticalPluginLoaderFactory
{
    public static IVerticalPluginLoader Create(
        string? pluginPath,
        string? pluginRoot = null,
        string? verticalId = null)
    {
        if (!string.IsNullOrWhiteSpace(pluginPath))
            return new AlcVerticalPluginLoader(pluginPath);

        if (!string.IsNullOrWhiteSpace(pluginRoot) && !string.IsNullOrWhiteSpace(verticalId))
            return new AlcVerticalPluginLoader(Path.Combine(pluginRoot, verticalId));

        throw new InvalidOperationException(
            "No vertical plugin was configured. Set VERTICAL_PLUGIN_PATH to a plugin DLL/folder, " +
            "or set VERTICAL_PLUGIN_ROOT and VERTICAL_ID so AgentForge can resolve '<root>/<vertical-id>'.");
    }
}
