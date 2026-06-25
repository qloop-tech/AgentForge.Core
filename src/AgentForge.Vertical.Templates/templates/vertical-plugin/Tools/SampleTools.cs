using AgentForge.Vertical.Template.Services;
using ModelContextProtocol.Server;

namespace AgentForge.Vertical.Template.Tools;

[McpServerToolType]
public sealed class SampleTools(SampleCatalogService catalog)
{
    [McpServerTool(Name = "search_sample_catalog", ReadOnly = true)]
    [Description("Search the sample vertical catalog by keyword.")]
    public string SearchSampleCatalog(
        [Description("Keyword to search for. Leave empty to return all items.")] string? query = null)
    {
        var items = catalog.GetAll();
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items
                .Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (items.Count == 0)
        {
            return "No catalog items matched the query.";
        }

        return string.Join(
            "\n",
            items.Select(item => $"* {item.Name} ({item.Id}) - {item.Description}"));
    }
}
