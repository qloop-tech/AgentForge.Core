using System.Net;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaApiClient(
    HttpClient httpClient,
    IConfiguration configuration,
    IOptions<OpenWaWebhookSecurityOptions> webhookSecurityOptions,
    ILogger<OpenWaApiClient> logger)
{
    public async Task SendTextAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var sessionRouteId = await ResolveSessionRouteIdAsync(cancellationToken).ConfigureAwait(false);

        await PostAsync(
                $"/api/sessions/{sessionRouteId}/messages/send-text",
                new OpenWaSendTextRequest(phoneNumber, message),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SendImageAsync(
        string phoneNumber,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);
        var sessionRouteId = await ResolveSessionRouteIdAsync(cancellationToken).ConfigureAwait(false);

        await PostAsync(
                $"/api/sessions/{sessionRouteId}/messages/send-image",
                new OpenWaSendImageRequest(phoneNumber, new OpenWaImagePayload(imageUrl), caption),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task EnsureDefaultSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await ResolveTargetSessionAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            logger.LogWarning("OpenWA has no sessions available. Create or connect a session from the OpenWA dashboard first.");
            return;
        }

        switch (session.Status?.Trim().ToUpperInvariant())
        {
            case "CONNECTED":
            case "CONNECTING":
                return;
            case "SCAN_QR":
            case "INITIALIZING":
                logger.LogInformation("OpenWA default session is waiting for QR onboarding. Status: {Status}", session.Status);
                return;
            case "DISCONNECTED":
            case "FAILED":
            case "STOPPED":
                await StartSessionAsync(ToSessionRouteId(session), cancellationToken).ConfigureAwait(false);
                return;
            default:
                logger.LogInformation("OpenWA default session is in status {Status}", session.Status ?? "<unknown>");
                return;
        }
    }

    public async Task ConfigureWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);

        await EnsureDefaultSessionAsync(cancellationToken).ConfigureAwait(false);
        var sessionRouteId = await ResolveSessionRouteIdAsync(cancellationToken).ConfigureAwait(false);

        var existingWebhooks = await GetWebhooksAsync(sessionRouteId, cancellationToken).ConfigureAwait(false);
        var matchingWebhook = existingWebhooks.FirstOrDefault(existingWebhook =>
            string.Equals(existingWebhook.Url, webhookUrl, StringComparison.OrdinalIgnoreCase));

        foreach (var existingWebhook in existingWebhooks)
        {
            if (!string.Equals(existingWebhook.Identifier, matchingWebhook?.Identifier, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(existingWebhook.Identifier))
            {
                await DeleteWebhookAsync(sessionRouteId, existingWebhook.Identifier!, cancellationToken).ConfigureAwait(false);
            }
        }

        if (matchingWebhook is not null)
        {
            return;
        }

        var secret = webhookSecurityOptions.Value.Secret;
        var request = new OpenWaWebhookRegistration(
            webhookUrl,
            string.IsNullOrWhiteSpace(secret) ? null : secret);

        await PostAsync($"/api/sessions/{sessionRouteId}/webhooks", request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveSessionRouteIdAsync(CancellationToken cancellationToken)
    {
        var session = await ResolveTargetSessionAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            throw new InvalidOperationException("No OpenWA session is available. Create or connect a session in the OpenWA dashboard.");
        }

        return ToSessionRouteId(session);
    }

    private async Task<OpenWaSession?> ResolveTargetSessionAsync(CancellationToken cancellationToken)
    {
        var sessions = await GetSessionsAsync(cancellationToken).ConfigureAwait(false);
        if (sessions.Count == 0)
        {
            return null;
        }

        var configuredSessionName = configuration["OPENWA_SESSION_NAME"];
        if (!string.IsNullOrWhiteSpace(configuredSessionName))
        {
            var matchedConfiguredSession = sessions.FirstOrDefault(
                session => string.Equals(session.Name, configuredSessionName, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(session.Id, configuredSessionName, StringComparison.OrdinalIgnoreCase));
            if (matchedConfiguredSession is not null)
            {
                return matchedConfiguredSession;
            }

            logger.LogWarning(
                "Configured OPENWA_SESSION_NAME '{SessionName}' was not found. Create or connect this session in OpenWA.",
                configuredSessionName);
            return null;
        }

        return sessions.FirstOrDefault(session =>
                   string.Equals(session.Name, "default", StringComparison.OrdinalIgnoreCase) && IsSessionReady(session))
               ?? sessions.FirstOrDefault(IsSessionReady)
               ?? sessions.FirstOrDefault(IsSessionStarting)
               ?? sessions.FirstOrDefault();
    }

    private async Task<IReadOnlyList<OpenWaSession>> GetSessionsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/api/sessions", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadBodyAsync<List<OpenWaSession>>(response, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static bool IsSessionReady(OpenWaSession session)
        => session.Status?.Trim().ToUpperInvariant() is "READY" or "CONNECTED";

    private static bool IsSessionStarting(OpenWaSession session)
        => session.Status?.Trim().ToUpperInvariant() is "CONNECTING" or "INITIALIZING" or "AUTHENTICATING";

    private static string ToSessionRouteId(OpenWaSession session)
    {
        var routeId = !string.IsNullOrWhiteSpace(session.Id)
            ? session.Id
            : session.Name;

        if (string.IsNullOrWhiteSpace(routeId))
        {
            throw new InvalidOperationException("OpenWA returned a session without id/name.");
        }

        return Uri.EscapeDataString(routeId);
    }

    private async Task StartSessionAsync(string sessionRouteId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync($"/api/sessions/{sessionRouteId}/start", content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<IReadOnlyList<OpenWaWebhookDefinition>> GetWebhooksAsync(string sessionRouteId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/api/sessions/{sessionRouteId}/webhooks", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        return await ReadBodyAsync<List<OpenWaWebhookDefinition>>(response, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private async Task DeleteWebhookAsync(string sessionRouteId, string webhookId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync(
                $"/api/sessions/{sessionRouteId}/webhooks/{Uri.EscapeDataString(webhookId)}",
                cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        logger.LogWarning("Failed to delete existing OpenWA webhook {WebhookId}. Status code: {StatusCode}", webhookId, response.StatusCode);
    }

    private async Task PostAsync<TRequest>(string path, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bodyText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return default;
        }

        using var document = JsonDocument.Parse(bodyText);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataElement))
        {
            return dataElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? default
                : dataElement.Deserialize<T>(JsonSerializerOptions.Web);
        }

        return root.Deserialize<T>(JsonSerializerOptions.Web);
    }
}
