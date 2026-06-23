using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Services;

public sealed record ResolvedMediaAsset(string Url);

public sealed class VerticalMediaAssetResolver(
    IVerticalDescriptor verticalDescriptor,
    IConfiguration configuration)
{
    private static readonly char[] InvalidLocalPathCharacters = ['\\', ':', '<', '>', '|', '"', '?', '*'];

    public bool TryResolve(OutboundMediaMarker marker, out ResolvedMediaAsset asset, out string reason)
    {
        asset = new ResolvedMediaAsset(string.Empty);
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(marker.Url))
        {
            reason = "The media marker did not contain a URL.";
            return false;
        }

        if (Uri.TryCreate(marker.Url, UriKind.Absolute, out var absoluteUri))
        {
            if (!IsSameOriginVerticalAsset(absoluteUri))
            {
                reason = $"The media URL is not a safe vertical asset URL: {marker.Url}";
                return false;
            }

            asset = new ResolvedMediaAsset(absoluteUri.ToString());
            return true;
        }

        if (!IsSafeLocalAssetPath(marker.Url))
        {
            reason = $"The media path is not under the active vertical asset prefix: {marker.Url}";
            return false;
        }

        var baseUrl = PublicWebhookUrlResolver.GetBaseUrl(configuration);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            reason = "WEBHOOK_BASE_URL or WEBHOOK_HTTP must be configured before sending local media assets.";
            return false;
        }

        asset = new ResolvedMediaAsset($"{baseUrl.TrimEnd('/')}/{marker.Url.TrimStart('/')}");
        return true;
    }

    private bool IsSameOriginVerticalAsset(Uri mediaUri)
    {
        var publicBase = PublicWebhookUrlResolver.GetBaseUrl(configuration);
        if (string.IsNullOrWhiteSpace(publicBase) || !Uri.TryCreate(publicBase.TrimEnd('/'), UriKind.Absolute, out var baseUri))
        {
            return false;
        }

        return string.Equals(mediaUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mediaUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            && mediaUri.Port == baseUri.Port
            && IsSafeLocalAssetPath(mediaUri.AbsolutePath);
    }

    private bool IsSafeLocalAssetPath(string path)
    {
        var normalizedPrefix = "/" + verticalDescriptor.AssetPathPrefix.Trim('/') + "/";
        var normalizedPath = "/" + path.TrimStart('/');

        return normalizedPath.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)
            && !normalizedPath.Contains("..", StringComparison.Ordinal)
            && !normalizedPath.Contains("//", StringComparison.Ordinal)
            && normalizedPath.IndexOfAny(InvalidLocalPathCharacters) < 0;
    }
}
