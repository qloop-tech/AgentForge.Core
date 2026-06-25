namespace AgentForge.Vertical.Template;

internal static class SamplePluginPaths
{
    public static string Root { get; } =
        Path.GetDirectoryName(typeof(SamplePluginPaths).Assembly.Location)
        ?? AppContext.BaseDirectory;

    public static string Configuration => Path.Combine(Root, "Configuration");

    public static string Data => Path.Combine(Root, "Data");

    public static string Assets => Path.Combine(Root, "Assets");
}
