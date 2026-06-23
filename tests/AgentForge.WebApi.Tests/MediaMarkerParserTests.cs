using AgentForge.WebApi.Services;

namespace AgentForge.WebApi.Tests;

public sealed class MediaMarkerParserTests
{
    [Fact]
    public void Parse_extracts_supported_media_markers_and_strips_them_from_text()
    {
        var parser = new MediaMarkerParser();

        var result = parser.Parse("""
            {{image:images/tours/kerala/1.jpg|Kerala}}
            {{video:videos/kerala-preview.mp4|Preview}}
            {{audio:audio/kerala-audio-brief.mp3|brief.mp3}}
            {{document:documents/kerala-brochure.pdf|brochure.pdf|Brochure}}
            {{sticker:stickers/aria-travel.png}}
            {{location:10.152,76.4019|Cochin Airport|Airport Road}}
            {{contact:Aria Travel Desk|+919999999999}}
            Here are the details.
            """);

        Assert.Equal("Here are the details.", result.Text);
        Assert.Equal(7, result.Markers.Count);
        Assert.Equal(OutboundMediaKind.Image, result.Markers[0].Kind);
        Assert.Equal("Kerala", result.Markers[0].Caption);
        Assert.Equal(OutboundMediaKind.Location, result.Markers[5].Kind);
        Assert.Equal(10.152, result.Markers[5].Latitude);
        Assert.Equal("Cochin Airport", result.Markers[5].Label);
        Assert.Equal(OutboundMediaKind.Contact, result.Markers[6].Kind);
        Assert.Equal("+919999999999", result.Markers[6].ContactNumber);
    }

    [Fact]
    public void Parse_ignores_invalid_location_and_contact_markers()
    {
        var parser = new MediaMarkerParser();

        var result = parser.Parse("{{location:not-a-coordinate|Bad}}{{contact:Only Name}}Plain text");

        Assert.Equal("Plain text", result.Text);
        Assert.Empty(result.Markers);
    }
}
