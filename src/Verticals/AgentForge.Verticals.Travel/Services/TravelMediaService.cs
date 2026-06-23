using System.Text.Json;
using AgentForge.Verticals.Travel.Models;

namespace AgentForge.Verticals.Travel.Services;

public sealed class TravelMediaService
{
    private readonly List<TravelMediaItem> _items;

    public TravelMediaService()
    {
        var json = TravelDataFiles.ReadAllText("TravelMedia.json");
        _items = JsonSerializer.Deserialize<List<TravelMediaItem>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    public IReadOnlyList<TravelMediaItem> Search(string? destination = null, string? purpose = null, string? mediaType = null)
    {
        var results = _items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(destination))
        {
            results = results.Where(item =>
                item.Destination.Equals("All", StringComparison.OrdinalIgnoreCase)
                || item.Destination.Contains(destination, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            results = results.Where(item => item.Purpose.Contains(purpose, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            results = results.Where(item => item.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase));
        }

        return results.ToList();
    }

    public static string ToMarker(TravelMediaItem item, string assetPathPrefix)
    {
        var url = item.Url is null
            ? null
            : $"{assetPathPrefix.TrimEnd('/')}/{item.Url.TrimStart('/')}";

        return item.MediaType.ToLowerInvariant() switch
        {
            "image" when url is not null => FormatMarker("image", url, item.Caption),
            "video" when url is not null => FormatMarker("video", url, item.Caption),
            "audio" when url is not null => FormatMarker("audio", url, item.Filename),
            "document" when url is not null => FormatMarker("document", url, item.Filename, item.Caption),
            "sticker" when url is not null => FormatMarker("sticker", url),
            "location" when item.Latitude.HasValue && item.Longitude.HasValue => FormatMarker(
                "location",
                $"{item.Latitude.Value:R},{item.Longitude.Value:R}",
                item.Label,
                item.Address),
            "contact" when !string.IsNullOrWhiteSpace(item.ContactName) && !string.IsNullOrWhiteSpace(item.ContactNumber) =>
                FormatMarker("contact", item.ContactName, item.ContactNumber),
            _ => string.Empty
        };
    }

    private static string FormatMarker(string kind, params string?[] parts)
        => $"{{{{{kind}:{string.Join('|', parts.Where(part => !string.IsNullOrWhiteSpace(part)))}}}}}";
}
