namespace Waha.WebApi.Services;

/// <summary>
/// Routes inbound WAHA webhook events to the appropriate handler.
/// Phase 2 keyword-based routing — will be replaced by AI agent in Phase 3.
/// </summary>
public sealed class MessageRouter(
    TravelBotHandler travelBot,
    FeedbackHandler feedbackHandler,
    ILogger<MessageRouter> logger)
{
    private static readonly string[] TravelKeywords =
        ["tour", "trip", "travel", "holiday", "vacation", "package", "itinerary", "book", "booking"];

    private static readonly string[] FeedbackKeywords =
        ["feedback", "review", "rate", "rating", "experience"];

    public async Task RouteAsync(WahaWebhookPayload payload, CancellationToken ct = default)
    {
        switch (payload.Event)
        {
            case "message":
                await HandleMessageAsync(payload, ct).ConfigureAwait(false);
                break;

            case "poll.vote":
                // poll.vote is not used with the text-based rating system; kept for future WAHA Plus upgrade
                logger.LogDebug("poll.vote event received (not handled in free tier)");
                break;

            case "session.status":
                logger.LogInformation("Session status changed: {Payload}", payload.Payload);
                break;

            default:
                logger.LogDebug("Unhandled event type: {Event}", payload.Event);
                break;
        }
    }

    private async Task HandleMessageAsync(WahaWebhookPayload payload, CancellationToken ct)
    {
        if (payload.Payload is null)
            return;

        var message = payload.Payload.Value.Deserialize<WahaMessage>(JsonSerializerOptions.Web);

        if (message is null || message.FromMe || string.IsNullOrWhiteSpace(message.Body))
            return;

        var body = message.Body.ToLowerInvariant();

        logger.LogInformation("Routing message from {From}: {Body}", message.From, message.Body);

        // Priority 1: user is responding to a pending feedback rating prompt
        if (feedbackHandler.IsPendingFeedback(message.From) && body.Trim() is "1" or "2" or "3" or "4" or "5")
        {
            await feedbackHandler.HandleRatingResponseAsync(message.From, body.Trim(), ct).ConfigureAwait(false);
            return;
        }

        if (body == "ping")
        {
            await travelBot.SendEchoAsync(message.From, "pong 🏓", ct).ConfigureAwait(false);
            return;
        }

        // Feedback must be checked before EdTech: "feedback" contains "fee" (an EdTech keyword)
        if (ContainsAny(body, FeedbackKeywords))
        {
            await feedbackHandler.SendFeedbackPollAsync(message.From, ct).ConfigureAwait(false);
            return;
        }

        if (ContainsAny(body, TravelKeywords))
        {
            await travelBot.HandleInquiryAsync(message, ct).ConfigureAwait(false);
            return;
        }

        // Default: greeting / help message
        await travelBot.SendEchoAsync(message.From,
            "👋 Hi! I can help you with:\n" +
            "• *Tours & Travel* — ask about tour packages, bookings\n" +
            "• *Feedback* — share your experience after your trip\n\n" +
            "Type your query and I'll assist you!", ct).ConfigureAwait(false);
    }

    private static bool ContainsAny(string text, string[] keywords) =>
        keywords.Any(text.Contains);
}
