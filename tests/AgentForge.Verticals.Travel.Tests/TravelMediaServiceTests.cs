using System.Diagnostics.CodeAnalysis;
using AgentForge.Verticals.Travel.Services;

namespace AgentForge.Verticals.Travel.Tests;

[ExcludeFromCodeCoverage]
public sealed class TravelMediaServiceTests
{
    [Fact]
    public void Search_returns_approved_media_markers_with_vertical_asset_prefix()
    {
        var service = new TravelMediaService();

        var media = service.Search(destination: "Kerala", purpose: "brochure", mediaType: "document").Single();
        var marker = TravelMediaService.ToMarker(media, "/images/");

        Assert.Equal("{{document:/images/documents/kerala-brochure.pdf|kerala-brochure.pdf|Kerala package brochure with itinerary highlights.}}", marker);
    }

    [Fact]
    public void Search_includes_shared_contact_media_for_specific_destinations()
    {
        var service = new TravelMediaService();

        var media = service.Search(destination: "Goa", purpose: "sales-contact", mediaType: "contact").Single();
        var marker = TravelMediaService.ToMarker(media, "/images/");

        Assert.Equal("{{contact:Aria Travel Desk|+919999999999}}", marker);
    }

    [Theory]
    [InlineData("destination-preview", "video", "{{video:/images/videos/kerala-preview.mp4|A quick preview of the Kerala backwaters experience.}}")]
    [InlineData("audio-briefing", "audio", "{{audio:/images/audio/kerala-audio-brief.mp3|kerala-audio-brief.mp3}}")]
    [InlineData("brochure", "document", "{{document:/images/documents/kerala-brochure.pdf|kerala-brochure.pdf|Kerala package brochure with itinerary highlights.}}")]
    [InlineData("meeting-point", "location", "{{location:10.152,76.4019|Cochin International Airport|Airport Road, Kochi, Kerala}}")]
    [InlineData("sticker", "sticker", "{{sticker:/images/stickers/aria-travel.png}}")]
    public void Search_returns_approved_non_tour_media_marker_formats(string purpose, string mediaType, string expectedMarker)
    {
        var service = new TravelMediaService();

        var media = service.Search(destination: "Kerala", purpose: purpose, mediaType: mediaType).First();
        var marker = TravelMediaService.ToMarker(media, "/images/");

        Assert.Equal(expectedMarker, marker);
    }

    [Fact]
    public void Travel_assets_are_copied_with_the_vertical_assembly()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(TravelMediaService).Assembly.Location);
        Assert.NotNull(assemblyDirectory);

        var assetRoot = Path.Combine(assemblyDirectory!, "Assets");

        Assert.True(File.Exists(Path.Combine(assetRoot, "images", "tours", "kerala", "1.jpg")));
        Assert.True(File.Exists(Path.Combine(assetRoot, "images", "videos", "kerala-preview.mp4")));
        Assert.True(File.Exists(Path.Combine(assetRoot, "images", "audio", "kerala-audio-brief.mp3")));
        Assert.True(File.Exists(Path.Combine(assetRoot, "images", "documents", "kerala-brochure.pdf")));
        Assert.True(File.Exists(Path.Combine(assetRoot, "images", "stickers", "aria-travel.png")));
    }
}
