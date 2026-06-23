using AgentForge.Verticals.Travel.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace AgentForge.Verticals.Travel.Tools;

[McpServerToolType]
public sealed class TravelMediaTools(
    TravelMediaService media,
    IOptions<TravelCustomerProfileOptions> options)
{
    [McpServerTool(Name = "get_travel_media", ReadOnly = true)]
    [Description("Return approved WhatsApp media markers for destination videos, audio briefs, brochures, location pins, contacts or stickers.")]
    public string GetTravelMedia(
        [Description("Destination name such as Goa, Kerala, Manali, Rajasthan or Andaman. Use 'All' for business-wide media.")] string? destination = null,
        [Description("Purpose such as destination-preview, brochure, audio-briefing, meeting-point, sales-contact or sticker.")] string? purpose = null,
        [Description("Media type: image, video, audio, document, sticker, location or contact.")] string? mediaType = null)
    {
        var results = media.Search(destination, purpose, mediaType);
        if (results.Count == 0)
        {
            return "No approved media assets found for that request.";
        }

        var lines = results.Select(item =>
        {
            var marker = TravelMediaService.ToMarker(item, options.Value.AssetPathPrefix);
            return string.IsNullOrWhiteSpace(marker)
                ? $"• {item.Title} ({item.MediaType})"
                : $"• {item.Title} ({item.MediaType})\n  {marker}";
        });

        return $"Approved media assets:\n{string.Join("\n", lines)}";
    }
}
