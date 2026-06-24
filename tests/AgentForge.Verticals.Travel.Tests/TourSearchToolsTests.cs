using System.Diagnostics.CodeAnalysis;
using AgentForge.Verticals.Travel.Services;
using AgentForge.Verticals.Travel.Tools;

namespace AgentForge.Verticals.Travel.Tests;

[ExcludeFromCodeCoverage]
public sealed class TourSearchToolsTests
{
    [Fact]
    public void GetTourDetails_without_image_opt_in_returns_text_only_details()
    {
        var tools = new TourSearchTools(new TourCatalogService());

        var result = tools.GetTourDetails("KERALA-BACKWATERS");

        Assert.Contains("*Kerala Backwaters & Beaches*", result);
        Assert.Contains("📅 *Availability:*", result);
        Assert.DoesNotContain("{{image:", result);
    }

    [Fact]
    public void GetTourDetails_with_image_opt_in_returns_image_markers()
    {
        var tools = new TourSearchTools(new TourCatalogService());

        var result = tools.GetTourDetails("KERALA-BACKWATERS", includeImages: true);

        Assert.Contains("{{image:images/tours/kerala/1.jpg|Kerala Backwaters & Beaches}}", result);
        Assert.Contains("*Kerala Backwaters & Beaches*", result);
    }
}
