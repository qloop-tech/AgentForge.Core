using System.Text.Json;

namespace Waha.WebApi.Handlers;

public record TourPackage(
    string Id,
    string Name,
    string Destination,
    string Duration,
    decimal Price,
    string Currency,
    string[] Tags,
    string[] Highlights,
    string ImageUrl,
    int Slots
);

public sealed class TravelBotHandler(
    WahaApiClient wahaClient,
    ILogger<TravelBotHandler> logger)
{
    private static readonly Lazy<TourPackage[]> Catalog = new(LoadCatalog);

    public Task SendEchoAsync(string chatId, string text, CancellationToken ct = default) =>
        wahaClient.SendTextAsync(chatId, text, ct);

    public async Task HandleInquiryAsync(WahaMessage message, CancellationToken ct = default)
    {
        var chatId = message.From;
        var query = message.Body?.ToLowerInvariant() ?? string.Empty;

        var matches = Catalog.Value
            .Where(t => t.Tags.Any(tag => query.Contains(tag)) ||
                        t.Destination.ToLowerInvariant().Contains(query) ||
                        t.Name.ToLowerInvariant().Contains(query))
            .Take(3)
            .ToArray();

        if (matches.Length == 0)
            matches = Catalog.Value.Take(3).ToArray();

        await wahaClient.SendTextAsync(chatId,
            $"✈️ *Great choice! Here are our top tour packages for you:*", ct).ConfigureAwait(false);

        foreach (var tour in matches)
        {
            var text =
                $"🗺️ *{tour.Name}*\n" +
                $"📍 {tour.Destination}\n" +
                $"🗓️ {tour.Duration}\n" +
                $"💰 ₹{tour.Price:N0} per person\n" +
                $"🎯 Highlights:\n" +
                string.Join("\n", tour.Highlights.Take(3).Select(h => $"  • {h}")) + "\n" +
                $"🪑 Only *{tour.Slots} slots* left!\n";

            await wahaClient.SendTextAsync(chatId, text, ct).ConfigureAwait(false);
        }

        await wahaClient.SendTextAsync(chatId,
            "📞 *Ready to book?* Reply with the *tour name* or type *book* to get started!\n" +
            "📋 Type *all tours* to see our full catalog.", ct).ConfigureAwait(false);
    }

    public async Task SendItineraryAsync(string chatId, string tourId, CancellationToken ct = default)
    {
        var tour = Catalog.Value.FirstOrDefault(t => t.Id == tourId);

        if (tour is null)
        {
            logger.LogWarning("Tour {TourId} not found for itinerary request", tourId);
            return;
        }

        var itinerary =
            $"📋 *Your Itinerary — {tour.Name}*\n\n" +
            $"📍 *Destination:* {tour.Destination}\n" +
            $"🗓️ *Duration:* {tour.Duration}\n\n" +
            $"*What's Included:*\n" +
            string.Join("\n", tour.Highlights.Select((h, i) => $"Day {i + 1}: {h}")) + "\n\n" +
            "📞 Emergency Contact: +91 98765 43210\n" +
            "🏨 Hotel confirmation will be shared 48 hours before departure.\n\n" +
            "Have a wonderful trip! 🎉";

        await wahaClient.SendTextAsync(chatId, itinerary, ct).ConfigureAwait(false);
    }

    public async Task SendDepartureReminderAsync(string chatId, string tourName, int daysUntilDeparture, CancellationToken ct = default)
    {
        var message = daysUntilDeparture switch
        {
            7 => $"⏰ *7 Days to Go!*\n\nYour *{tourName}* trip is just 7 days away! 🎉\n\n" +
                 "✅ *Checklist:*\n• Check your ID/Passport validity\n• Pack weather-appropriate clothes\n" +
                 "• Download offline maps\n• Charge all devices\n• Inform your bank about travel",
            1 => $"🚀 *Tomorrow is the day!*\n\n*{tourName}* — Departure tomorrow!\n\n" +
                 "📌 Your driver will arrive at *6:00 AM*\n" +
                 "📞 Driver contact: +91 98765 43210\n" +
                 "🎒 Keep your documents handy\n" +
                 "Have an amazing trip! ✈️",
            0 => $"🎉 *Today's the Day! Your {tourName} adventure begins!*\n\n" +
                 "Safe travels and make wonderful memories! 📸",
            _ => $"⏰ *{daysUntilDeparture} days to your {tourName} trip!* Are you excited? 🎒"
        };

        await wahaClient.SendTextAsync(chatId, message, ct).ConfigureAwait(false);
    }

    private static TourPackage[] LoadCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "TourCatalog.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TourPackage[]>(json, JsonSerializerOptions.Web) ?? [];
    }
}
