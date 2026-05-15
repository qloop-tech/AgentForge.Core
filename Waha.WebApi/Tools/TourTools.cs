using System.Text.Json;
using Waha.WebApi.Handlers;

namespace Waha.WebApi.Tools;

/// <summary>
/// AI tool functions for the travel bot.
/// Used by Microsoft Agent Framework (Phase 3) via AIFunctionFactory.Create().
/// Pattern matches NetworkTools.cs in the sample app.
/// </summary>
public static class TourTools
{
    [Description("Search available tour packages by destination, tags, or keywords. Returns matching tours as JSON.")]
    public static string SearchTours(
        [Description("Search query — destination name, trip type (beach, adventure, family), or month")] string query)
    {
        var catalog = LoadCatalog();
        var q = query.ToLowerInvariant();

        var matches = catalog
            .Where(t => t.Tags.Any(tag => q.Contains(tag)) ||
                        t.Destination.ToLowerInvariant().Contains(q) ||
                        t.Name.ToLowerInvariant().Contains(q))
            .Take(3)
            .ToArray();

        if (matches.Length == 0)
            matches = catalog.Take(3).ToArray();

        return JsonSerializer.Serialize(matches.Select(t => new
        {
            t.Id, t.Name, t.Destination, t.Duration,
            Price = $"₹{t.Price:N0} per person",
            t.Slots,
            Highlights = t.Highlights.Take(3)
        }), JsonSerializerOptions.Web);
    }

    [Description("Get full details of a specific tour package by its ID.")]
    public static string GetTourDetails(
        [Description("The tour package ID, e.g. GOA-DEC or MANALI-WINTER")] string tourId)
    {
        var tour = LoadCatalog().FirstOrDefault(t => t.Id.Equals(tourId, StringComparison.OrdinalIgnoreCase));

        if (tour is null)
            return $"Tour '{tourId}' not found.";

        return JsonSerializer.Serialize(tour, JsonSerializerOptions.Web);
    }

    [Description("Check available slots for a tour package.")]
    public static string CheckAvailability(
        [Description("The tour package ID to check")] string tourId)
    {
        var tour = LoadCatalog().FirstOrDefault(t => t.Id.Equals(tourId, StringComparison.OrdinalIgnoreCase));

        return tour is null
            ? $"Tour '{tourId}' not found."
            : tour.Slots > 0
                ? $"{tour.Name} has {tour.Slots} slots available."
                : $"{tour.Name} is fully booked. Please check other packages.";
    }

    private static TourPackage[] LoadCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "TourCatalog.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TourPackage[]>(json, JsonSerializerOptions.Web) ?? [];
    }
}
