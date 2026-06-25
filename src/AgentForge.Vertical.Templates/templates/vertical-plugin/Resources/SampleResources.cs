using AgentForge.Vertical.Template.Services;
using ModelContextProtocol.Server;

namespace AgentForge.Vertical.Template.Resources;

[McpServerResourceType]
public sealed class SampleResources(SampleCatalogService catalog)
{
    [McpServerResource(UriTemplate = "sample://catalog", Name = "Sample Catalog", MimeType = "text/plain")]
    [Description("Complete sample vertical catalog.")]
    public string GetCatalog()
    {
        var items = catalog.GetAll();
        return items.Count == 0
            ? "Sample catalog is empty."
            : string.Join("\n", items.Select(item => $"{item.Id}: {item.Name} - {item.Description}"));
    }
}
