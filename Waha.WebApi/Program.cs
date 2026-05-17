using Waha.WebApi.Endpoints;
using Waha.WebApi.Handlers;
using Waha.WebApi.Scheduling;
using Waha.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ─── WAHA HTTP Client ─────────────────────────────────────────────────────────
// WAHA's sendText waits for WhatsApp delivery (can take >10s), so we override
// Aspire's default 10s per-attempt timeout with a generous custom pipeline.
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<WahaApiClient>(client =>
{
    client.BaseAddress = new Uri("http://waha");
    var apiKey = builder.Configuration["WAHA_API_KEY"] ?? string.Empty;
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
})
.RemoveAllResilienceHandlers()
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
    options.Retry.MaxRetryAttempts = 2;
});

// ─── MCP Server HTTP Client (Aspire service discovery) ────────────────────────
// MCP StreamableHttp holds long-lived SSE connections during multi-turn AI
// conversations — remove standard resilience so no per-attempt timeout applies.
builder.Services.AddHttpClient("mcpserver", client =>
{
    client.BaseAddress = new Uri("http://mcpserver");
    client.Timeout = TimeSpan.FromMinutes(10);
})
.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

// ─── Azure OpenAI Chat Client ─────────────────────────────────────────────────
builder.AddAzureChatCompletionsClient(connectionName: "ai-foundry")
    .AddChatClient("gpt-5.4-mini");

// ─── AI Agent Services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<McpClientProvider>();
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddSingleton<TravelAgentFactory>();
builder.Services.AddScoped<AgentChatService>();

// ─── Bot Handlers (used by scheduler for reminders/post-trip) ─────────────────
builder.Services.AddScoped<TravelBotHandler>();
builder.Services.AddScoped<FeedbackHandler>();

// ─── Background Services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<SchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SchedulerService>());
builder.Services.AddSingleton<WebhookRegistrationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookRegistrationService>());

// ─── JSON serialisation ───────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapWebhookEndpoints();

app.Run();
