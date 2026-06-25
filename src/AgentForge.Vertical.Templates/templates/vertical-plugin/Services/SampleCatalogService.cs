using System.Text.Json;
using AgentForge.Vertical.Template;

namespace AgentForge.Vertical.Template.Services;

public sealed class SampleCatalogService
{
    private readonly IReadOnlyList<SampleCatalogItem> _items;

    public SampleCatalogService()
    {
        var path = Path.Combine(SamplePluginPaths.Data, "sample-catalog.json");
        if (!File.Exists(path))
        {
            _items = [];
            return;
        }

        var json = File.ReadAllText(path);
        _items = JsonSerializer.Deserialize<List<SampleCatalogItem>>(json, JsonSerializerOptions.Web) ?? [];
    }

    public IReadOnlyList<SampleCatalogItem> GetAll() => _items;
}

public sealed record SampleCatalogItem(
    string Id,
    string Name,
    string Description);
