using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AgentForge.WebApi.Models;

namespace AgentForge.WebApi.Tests;

[ExcludeFromCodeCoverage]
public sealed class OpenWaMessageTests
{
    [Fact]
    public void HasUnsupportedInboundMedia_returns_true_when_has_media_is_true()
    {
        var message = Deserialize("""{"chatId":"chat","body":"caption","hasMedia":true,"type":"chat"}""");

        Assert.True(message.HasUnsupportedInboundMedia());
    }

    [Fact]
    public void HasUnsupportedInboundMedia_returns_true_when_media_object_exists()
    {
        var message = Deserialize("""{"chatId":"chat","body":"caption","media":{"url":"https://example.test/image.jpg"},"type":"chat"}""");

        Assert.True(message.HasUnsupportedInboundMedia());
    }

    [Fact]
    public void HasUnsupportedInboundMedia_returns_true_when_location_object_exists()
    {
        var message = Deserialize("""{"chatId":"chat","location":{"latitude":10.152,"longitude":76.4019},"type":"chat"}""");

        Assert.True(message.HasUnsupportedInboundMedia());
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    [InlineData("audio")]
    [InlineData("ptt")]
    [InlineData("voice")]
    [InlineData("document")]
    [InlineData("sticker")]
    [InlineData("location")]
    [InlineData("contact")]
    [InlineData("vcard")]
    public void HasUnsupportedInboundMedia_returns_true_for_media_message_types(string type)
    {
        var message = Deserialize($$"""{"chatId":"chat","body":"caption","type":"{{type}}"}""");

        Assert.True(message.HasUnsupportedInboundMedia());
    }

    [Fact]
    public void HasUnsupportedInboundMedia_returns_false_for_plain_chat_text()
    {
        var message = Deserialize("""{"chatId":"chat","body":"hello","type":"chat"}""");

        Assert.False(message.HasUnsupportedInboundMedia());
    }

    private static OpenWaMessage Deserialize(string json)
        => JsonSerializer.Deserialize<OpenWaMessage>(json, JsonSerializerOptions.Web)
           ?? throw new InvalidOperationException("Test message did not deserialize.");
}
