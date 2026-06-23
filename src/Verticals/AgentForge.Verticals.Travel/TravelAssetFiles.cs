namespace AgentForge.Verticals.Travel;

internal static class TravelAssetFiles
{
    private const string AssetDirectoryName = "Assets";

    public static string AssetRoot { get; } = Path.Combine(
        Path.GetDirectoryName(typeof(TravelAssetFiles).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve the travel vertical assembly directory."),
        AssetDirectoryName);

    public static void EnsureAssetDirectoryExists()
    {
        if (!Directory.Exists(AssetRoot))
        {
            throw new DirectoryNotFoundException($"Travel vertical assets were not found at '{AssetRoot}'.");
        }
    }
}
