using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentForge.WebApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentForge.WebApi.Tests;

[ExcludeFromCodeCoverage]
public sealed class OpenWaApiClientTests
{
    [Fact]
    public async Task SendImageAsync_posts_flat_openwa_media_payload()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);

        await client.SendImageAsync(
            "919825318335@c.us",
            "https://public.example/images/tours/kerala/1.jpg",
            "Kerala",
            TestContext.Current.CancellationToken);

        Assert.Equal("/api/sessions/travel-bot/messages/send-image", handler.PostedPath);
        using var document = JsonDocument.Parse(handler.PostedJson);
        var root = document.RootElement;
        Assert.Equal("919825318335@c.us", root.GetProperty("chatId").GetString());
        Assert.Equal("https://public.example/images/tours/kerala/1.jpg", root.GetProperty("url").GetString());
        Assert.Equal("image/jpeg", root.GetProperty("mimetype").GetString());
        Assert.Equal("1.jpg", root.GetProperty("filename").GetString());
        Assert.Equal("Kerala", root.GetProperty("caption").GetString());
        Assert.False(root.TryGetProperty("image", out _));
    }

    [Fact]
    public async Task SendLocationAsync_posts_location_endpoint_payload()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);

        await client.SendLocationAsync(
            "chat",
            10.152,
            76.4019,
            "Airport",
            "Kochi",
            TestContext.Current.CancellationToken);

        Assert.Equal("/api/sessions/travel-bot/messages/send-location", handler.PostedPath);
        using var document = JsonDocument.Parse(handler.PostedJson);
        var root = document.RootElement;
        Assert.Equal("chat", root.GetProperty("chatId").GetString());
        Assert.Equal(10.152, root.GetProperty("latitude").GetDouble());
        Assert.Equal(76.4019, root.GetProperty("longitude").GetDouble());
        Assert.Equal("Airport", root.GetProperty("description").GetString());
        Assert.Equal("Kochi", root.GetProperty("address").GetString());
    }

    private static OpenWaApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openwa.example")
        };
        var configuration = new ConfigurationBuilder().Build();
        var securityOptions = Options.Create(new OpenWaWebhookSecurityOptions { Secret = "secret" });

        return new OpenWaApiClient(
            httpClient,
            configuration,
            securityOptions,
            NullLogger<OpenWaApiClient>.Instance);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public string PostedPath { get; private set; } = string.Empty;
        public string PostedJson { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/sessions")
            {
                return JsonResponse(new
                {
                    data = new[]
                    {
                        new { id = "travel-bot", name = "travel-bot", status = "CONNECTED" }
                    }
                });
            }

            if (request.Method == HttpMethod.Post)
            {
                PostedPath = request.RequestUri?.AbsolutePath ?? string.Empty;
                PostedJson = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonResponse(new { data = new { messageId = "m1", timestamp = 1 } });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse<T>(T value)
            => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(value)
            };
    }
}
