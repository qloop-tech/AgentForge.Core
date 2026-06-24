using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AgentForge.WebApi.Endpoints;
using AgentForge.WebApi.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentForge.WebApi.Tests;

[ExcludeFromCodeCoverage]
public sealed class WebhookEndpointTests
{
    [Fact]
    public async Task HandleInboundMessageAsync_sends_unsupported_notice_and_does_not_enqueue_media()
    {
        var recorder = new WebhookRecorder();
        var message = Deserialize("""{"chatId":"chat","body":"nice pic","type":"image"}""");

        await WebhookEndpoint.HandleInboundMessageAsync(
            message,
            dedupeKey: "dedupe-1",
            deliveryId: "delivery-1",
            dedupeRegistered: true,
            recorder.EnqueueAsync,
            recorder.SendTextAsync,
            recorder.RemoveDedupeAsync,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        Assert.Equal(["text:chat:Only Text is supported for now"], recorder.Calls);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_enqueues_plain_text_message()
    {
        var recorder = new WebhookRecorder();
        var message = Deserialize("""{"chatId":"chat","body":"hello","type":"chat"}""");

        await WebhookEndpoint.HandleInboundMessageAsync(
            message,
            dedupeKey: "dedupe-1",
            deliveryId: "delivery-1",
            dedupeRegistered: true,
            recorder.EnqueueAsync,
            recorder.SendTextAsync,
            recorder.RemoveDedupeAsync,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        Assert.Equal(["enqueue:chat:hello:dedupe-1:delivery-1"], recorder.Calls);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_rejects_image_caption_without_enqueuing_caption()
    {
        var recorder = new WebhookRecorder();
        var message = Deserialize("""{"chatId":"chat","body":"caption should not reach llm","hasMedia":true,"type":"image"}""");

        await WebhookEndpoint.HandleInboundMessageAsync(
            message,
            dedupeKey: "dedupe-1",
            deliveryId: "delivery-1",
            dedupeRegistered: true,
            recorder.EnqueueAsync,
            recorder.SendTextAsync,
            recorder.RemoveDedupeAsync,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        Assert.Equal(["text:chat:Only Text is supported for now"], recorder.Calls);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_removes_dedupe_key_when_unsupported_notice_fails()
    {
        var recorder = new WebhookRecorder { ThrowOnSend = true };
        var message = Deserialize("""{"chatId":"chat","type":"document"}""");

        await WebhookEndpoint.HandleInboundMessageAsync(
            message,
            dedupeKey: "dedupe-1",
            deliveryId: "delivery-1",
            dedupeRegistered: true,
            recorder.EnqueueAsync,
            recorder.SendTextAsync,
            recorder.RemoveDedupeAsync,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        Assert.Equal(["text:chat:Only Text is supported for now", "remove:dedupe-1"], recorder.Calls);
    }

    private static OpenWaMessage Deserialize(string json)
        => JsonSerializer.Deserialize<OpenWaMessage>(json, JsonSerializerOptions.Web)
           ?? throw new InvalidOperationException("Test message did not deserialize.");

    private sealed class WebhookRecorder
    {
        public List<string> Calls { get; } = [];
        public bool ThrowOnSend { get; init; }

        public Task EnqueueAsync(string phone, string body, string? dedupeKey, string? deliveryId, CancellationToken ct)
        {
            Calls.Add($"enqueue:{phone}:{body}:{dedupeKey}:{deliveryId}");
            return Task.CompletedTask;
        }

        public Task SendTextAsync(string chatId, string text, CancellationToken ct)
        {
            Calls.Add($"text:{chatId}:{text}");
            return ThrowOnSend ? Task.FromException(new InvalidOperationException("send failed")) : Task.CompletedTask;
        }

        public Task RemoveDedupeAsync(string? dedupeKey)
        {
            Calls.Add($"remove:{dedupeKey}");
            return Task.CompletedTask;
        }
    }
}
