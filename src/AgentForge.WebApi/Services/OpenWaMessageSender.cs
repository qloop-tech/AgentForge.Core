using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaMessageSender(OpenWaApiClient openWaApiClient) : IMessageSender
{
    public Task SendTextAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        => openWaApiClient.SendTextAsync(phoneNumber, message, cancellationToken);

    public Task SendImageAsync(
        string phoneNumber,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendImageAsync(phoneNumber, imageUrl, caption, cancellationToken);

    public Task SendVideoAsync(
        string phoneNumber,
        string videoUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendVideoAsync(phoneNumber, videoUrl, caption, cancellationToken);

    public Task SendAudioAsync(
        string phoneNumber,
        string audioUrl,
        string? filename = null,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendAudioAsync(phoneNumber, audioUrl, filename, cancellationToken);

    public Task SendDocumentAsync(
        string phoneNumber,
        string documentUrl,
        string? filename = null,
        string? caption = null,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendDocumentAsync(phoneNumber, documentUrl, filename, caption, cancellationToken);

    public Task SendLocationAsync(
        string phoneNumber,
        double latitude,
        double longitude,
        string? label = null,
        string? address = null,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendLocationAsync(phoneNumber, latitude, longitude, label, address, cancellationToken);

    public Task SendContactAsync(
        string phoneNumber,
        string contactName,
        string contactNumber,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendContactAsync(phoneNumber, contactName, contactNumber, cancellationToken);

    public Task SendStickerAsync(
        string phoneNumber,
        string stickerUrl,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendStickerAsync(phoneNumber, stickerUrl, cancellationToken);
}
