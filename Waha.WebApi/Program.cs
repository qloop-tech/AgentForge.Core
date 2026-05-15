using Waha.WebApi.Endpoints;
using Waha.WebApi.Handlers;
using Waha.WebApi.Scheduling;
using Waha.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ─── WAHA HTTP Client ─────────────────────────────────────────────────────────
// Uses Aspire service discovery: services__waha__http__0 is injected by AppHost
builder.Services.AddHttpClient<WahaApiClient>(client =>
{
    client.BaseAddress = new Uri("http://waha");   // Aspire service discovery name
    var apiKey = builder.Configuration["WAHA_API_KEY"] ?? string.Empty;
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

// ─── Bot Handlers ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<TravelBotHandler>();
builder.Services.AddScoped<FeedbackHandler>();
builder.Services.AddScoped<MessageRouter>();

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
