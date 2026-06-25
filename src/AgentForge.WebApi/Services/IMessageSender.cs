namespace AgentForge.WebApi.Services;

public interface IMessageSender
{
    Task SendTextAsync(string chatId, string text, CancellationToken ct = default);

    Task SendImageAsync(string chatId, string imageUrl, string? caption = null, CancellationToken ct = default);

    Task SendVideoAsync(string chatId, string videoUrl, string? caption = null, CancellationToken ct = default);

    Task SendAudioAsync(string chatId, string audioUrl, string? filename = null, CancellationToken ct = default);

    Task SendDocumentAsync(string chatId, string documentUrl, string? filename = null, string? caption = null, CancellationToken ct = default);

    Task SendLocationAsync(string chatId, double latitude, double longitude, string? label = null, string? address = null, CancellationToken ct = default);

    Task SendContactAsync(string chatId, string contactName, string contactNumber, CancellationToken ct = default);

    Task SendStickerAsync(string chatId, string stickerUrl, CancellationToken ct = default);
}
