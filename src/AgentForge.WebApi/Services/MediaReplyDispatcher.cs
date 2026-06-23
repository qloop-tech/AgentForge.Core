using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Services;

public sealed record MediaReplyDispatchOptions(TimeSpan MediaSendDelay)
{
    public static MediaReplyDispatchOptions Default { get; } = new(TimeSpan.FromMilliseconds(750));
}

public sealed class MediaReplyDispatcher(
    MediaMarkerParser parser,
    VerticalMediaAssetResolver assetResolver,
    IMessageSender messageSender,
    MediaReplyDispatchOptions options,
    ILogger<MediaReplyDispatcher> logger)
{
    public async Task DispatchAsync(string chatId, string rawReply, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chatId);

        var parsedReply = parser.Parse(rawReply);
        foreach (var marker in parsedReply.Markers)
        {
            var sent = await TrySendMarkerAsync(chatId, marker, ct).ConfigureAwait(false);
            if (sent && options.MediaSendDelay > TimeSpan.Zero)
            {
                await Task.Delay(options.MediaSendDelay, ct).ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(parsedReply.Text))
        {
            await messageSender.SendTextAsync(chatId, parsedReply.Text, ct).ConfigureAwait(false);
        }
    }

    private async Task<bool> TrySendMarkerAsync(string chatId, OutboundMediaMarker marker, CancellationToken ct)
    {
        try
        {
            switch (marker.Kind)
            {
                case OutboundMediaKind.Image:
                    return await TrySendResolvedMediaAsync(
                        marker,
                        asset => messageSender.SendImageAsync(chatId, asset.Url, marker.Caption, ct)).ConfigureAwait(false);

                case OutboundMediaKind.Video:
                    return await TrySendResolvedMediaAsync(
                        marker,
                        asset => messageSender.SendVideoAsync(chatId, asset.Url, marker.Caption, ct)).ConfigureAwait(false);

                case OutboundMediaKind.Audio:
                    return await TrySendResolvedMediaAsync(
                        marker,
                        asset => messageSender.SendAudioAsync(chatId, asset.Url, marker.Filename, ct)).ConfigureAwait(false);

                case OutboundMediaKind.Document:
                    return await TrySendResolvedMediaAsync(
                        marker,
                        asset => messageSender.SendDocumentAsync(chatId, asset.Url, marker.Filename, marker.Caption, ct)).ConfigureAwait(false);

                case OutboundMediaKind.Sticker:
                    return await TrySendResolvedMediaAsync(
                        marker,
                        asset => messageSender.SendStickerAsync(chatId, asset.Url, ct)).ConfigureAwait(false);

                case OutboundMediaKind.Location:
                    await messageSender.SendLocationAsync(
                        chatId,
                        marker.Latitude!.Value,
                        marker.Longitude!.Value,
                        marker.Label,
                        marker.Address,
                        ct).ConfigureAwait(false);
                    return true;

                case OutboundMediaKind.Contact:
                    await messageSender.SendContactAsync(
                        chatId,
                        marker.ContactName!,
                        marker.ContactNumber!,
                        ct).ConfigureAwait(false);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send {MediaKind} marker to {ChatId}: {Marker}", marker.Kind, chatId, marker.Raw);
            return false;
        }
    }

    private async Task<bool> TrySendResolvedMediaAsync(OutboundMediaMarker marker, Func<ResolvedMediaAsset, Task> send)
    {
        if (!assetResolver.TryResolve(marker, out var asset, out var reason))
        {
            logger.LogWarning("Skipping {MediaKind} marker because it is not a safe media asset. {Reason}", marker.Kind, reason);
            return false;
        }

        await send(asset).ConfigureAwait(false);
        return true;
    }
}
