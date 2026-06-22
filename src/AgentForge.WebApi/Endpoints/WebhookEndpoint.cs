using AgentForge.WebApi.Queue;
using Microsoft.AspNetCore.Mvc;

namespace AgentForge.WebApi.Endpoints;

public static class WebhookEndpoint
{
    private const int MaxWebhookBodyBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook", HandleWebhookAsync)
           .AllowAnonymous()
           .WithMetadata(new RequestSizeLimitAttribute(MaxWebhookBodyBytes))
           .WithName("ReceiveOpenWaWebhook");

        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
           // Admin endpoint for manual webhook registration — useful when the tunnel URL
           // isn't automatically injected (e.g., first start before tunnel warms up).
           // Usage: POST /admin/register-webhook?url=https://xxx.devtunnels.ms
           app.MapPost("/admin/register-webhook", RegisterWebhookAsync)
              .AllowAnonymous()
              .WithName("RegisterOpenWaWebhook");
        }

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        HttpRequest request,
        OpenWaWebhookSignatureValidator signatureValidator,
        OpenWaWebhookIdempotencyStore idempotencyStore,
        WhatsAppMessageQueue messageQueue,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(WebhookEndpoint));
        var validation = await signatureValidator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
           return Results.BadRequest("Missing or invalid webhook signature.");

        OpenWaWebhookPayload? payload;
        try
        {
           payload = JsonSerializer.Deserialize<OpenWaWebhookPayload>(validation.BodyBytes!, JsonSerializerOptions.Web);
        }
        catch (JsonException ex)
        {
           logger.LogWarning(ex, "Received OpenWA webhook with an invalid JSON payload");
           return Results.BadRequest("Invalid webhook payload.");
        }

        if (payload is null)
           return Results.BadRequest("Webhook payload is required.");

        logger.LogDebug("Received OpenWA event: {Event} from session: {Session}", payload.Event, payload.EffectiveSession);

        if (!string.Equals(payload.Event, "message.received", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(payload.Event, "message", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok();
        }

        var dedupeKey = payload.GetDedupeKey();
        try
        {
            if (!await idempotencyStore.TryRegisterAsync(dedupeKey, ct).ConfigureAwait(false))
            {
                return Results.Ok();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register OpenWA webhook dedupe key");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var dedupeRegistered = !string.IsNullOrWhiteSpace(dedupeKey);

        OpenWaMessage? message;
        try
        {
            message = payload.EventPayload?.Deserialize<OpenWaMessage>(JsonSerializerOptions.Web);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Received OpenWA message webhook with an invalid message payload");
            if (dedupeRegistered)
            {
                await idempotencyStore.RemoveAsync(dedupeKey).ConfigureAwait(false);
            }

            return Results.BadRequest("Invalid message payload.");
        }
        var phoneNumber = message?.GetSender();
        var body = message?.GetBody();
        if (message is null || message.FromMe == true || string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(body))
        {
            return Results.Ok();
        }

        try
        {
            await messageQueue.EnqueueAsync(phoneNumber, body, dedupeKey, payload.DeliveryId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (dedupeRegistered)
            {
                await idempotencyStore.RemoveAsync(dedupeKey).ConfigureAwait(false);
            }

            logger.LogError(ex, "Failed to enqueue OpenWA webhook message from {Phone}", phoneNumber);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok();
    }

    private static async Task<IResult> RegisterWebhookAsync(
        string url,
        WebhookRegistrationService registrationService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Results.BadRequest("url query parameter is required");

        await registrationService.RegisterAsync(url, ct).ConfigureAwait(false);
        return Results.Ok(new { registered = registrationService.RegisteredUrl });
    }
}
