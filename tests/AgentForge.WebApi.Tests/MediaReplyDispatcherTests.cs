using AgentForge.Verticals.Abstractions;
using AgentForge.WebApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentForge.WebApi.Tests;

public sealed class MediaReplyDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_sends_safe_media_in_order_then_text()
    {
        var sender = new RecordingMessageSender();
        var dispatcher = CreateDispatcher(sender);

        await dispatcher.DispatchAsync(
            "919825318335@c.us",
            "{{image:images/tours/kerala/1.jpg|Kerala}}{{location:10.152,76.4019|Airport|Kochi}}Text after markers",
            TestContext.Current.CancellationToken);

        Assert.Collection(
            sender.Calls,
            call => Assert.Equal("image:https://public.example/images/tours/kerala/1.jpg:Kerala", call),
            call => Assert.Equal("location:10.152:76.4019:Airport:Kochi", call),
            call => Assert.Equal("text:Text after markers", call));
    }

    [Fact]
    public async Task DispatchAsync_skips_external_media_but_still_sends_text()
    {
        var sender = new RecordingMessageSender();
        var dispatcher = CreateDispatcher(sender);

        await dispatcher.DispatchAsync(
            "chat",
            "{{image:https://evil.example/a.jpg|bad}}Still reply",
            TestContext.Current.CancellationToken);

        Assert.Equal(["text:Still reply"], sender.Calls);
    }

    private static MediaReplyDispatcher CreateDispatcher(RecordingMessageSender sender)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WEBHOOK_BASE_URL"] = "https://public.example"
            })
            .Build();
        var descriptor = new TestVerticalDescriptor();
        var resolver = new VerticalMediaAssetResolver(descriptor, configuration);

        return new MediaReplyDispatcher(
            new MediaMarkerParser(),
            resolver,
            sender,
            new MediaReplyDispatchOptions(TimeSpan.Zero),
            NullLogger<MediaReplyDispatcher>.Instance);
    }

    private sealed class TestVerticalDescriptor : IVerticalDescriptor
    {
        public string VerticalId => "test";
        public string DisplayName => "Test";
        public string AgentName => "Agent";
        public string AgentDescription => "Test agent";
        public string SystemPrompt => "Prompt";
        public string McpServerName => "mcp";
        public string AssetPathPrefix => "/images/";
        public string AssetRootPath => "/";
        public string PreviewTitle => "Preview";
        public string PreviewDescription => "Description";
    }

    private sealed class RecordingMessageSender : IMessageSender
    {
        public List<string> Calls { get; } = [];

        public Task SendTextAsync(string chatId, string text, CancellationToken ct = default)
        {
            Calls.Add($"text:{text}");
            return Task.CompletedTask;
        }

        public Task SendImageAsync(string chatId, string imageUrl, string? caption = null, CancellationToken ct = default)
        {
            Calls.Add($"image:{imageUrl}:{caption}");
            return Task.CompletedTask;
        }

        public Task SendVideoAsync(string chatId, string videoUrl, string? caption = null, CancellationToken ct = default)
        {
            Calls.Add($"video:{videoUrl}:{caption}");
            return Task.CompletedTask;
        }

        public Task SendAudioAsync(string chatId, string audioUrl, string? filename = null, CancellationToken ct = default)
        {
            Calls.Add($"audio:{audioUrl}:{filename}");
            return Task.CompletedTask;
        }

        public Task SendDocumentAsync(string chatId, string documentUrl, string? filename = null, string? caption = null, CancellationToken ct = default)
        {
            Calls.Add($"document:{documentUrl}:{filename}:{caption}");
            return Task.CompletedTask;
        }

        public Task SendLocationAsync(string chatId, double latitude, double longitude, string? label = null, string? address = null, CancellationToken ct = default)
        {
            Calls.Add($"location:{latitude}:{longitude}:{label}:{address}");
            return Task.CompletedTask;
        }

        public Task SendContactAsync(string chatId, string contactName, string contactNumber, CancellationToken ct = default)
        {
            Calls.Add($"contact:{contactName}:{contactNumber}");
            return Task.CompletedTask;
        }

        public Task SendStickerAsync(string chatId, string stickerUrl, CancellationToken ct = default)
        {
            Calls.Add($"sticker:{stickerUrl}");
            return Task.CompletedTask;
        }
    }
}
