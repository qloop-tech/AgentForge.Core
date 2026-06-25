using System.Diagnostics.CodeAnalysis;
using AgentForge.Verticals.Abstractions;

namespace AgentForge.Verticals.Travel.Tests;

[ExcludeFromCodeCoverage]
public sealed class TravelScheduledActionHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_scheduled_messages_without_sending_them()
    {
        var handler = new TravelScheduledActionHandler();
        var action = new ScheduledAction(
            "919825318335@c.us",
            TravelScheduledActionTypes.PostTripFeedback,
            "Kerala Backwaters",
            DateTimeOffset.UtcNow);

        var messages = await handler.HandleAsync(action, TestContext.Current.CancellationToken);

        Assert.Equal(2, messages.Count);
        Assert.All(messages, message => Assert.Equal("919825318335@c.us", message.ChatId));
        Assert.Contains("Welcome back", messages[0].Text);
        Assert.Contains("We'd love your feedback", messages[1].Text);
    }
}
