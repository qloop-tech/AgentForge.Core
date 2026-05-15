var builder = DistributedApplication.CreateBuilder(args);

// ─── WAHA Credentials ─────────────────────────────────────────────────────────
// Seed dev values with:
//   dotnet user-secrets set "Parameters:wahaApiKey" "devkey00000000000000000000000000"
//   dotnet user-secrets set "Parameters:wahaDashboardPassword" "devpassword123"
//   dotnet user-secrets set "Parameters:wahaSwaggerPassword" "devpassword123"
var wahaApiKey = builder.AddParameter("wahaApiKey", secret: true);
var wahaDashboardPassword = builder.AddParameter("wahaDashboardPassword", secret: true);
var wahaSwaggerPassword = builder.AddParameter("wahaSwaggerPassword", secret: true);

// ─── WAHA Container (custom Aspire integration) ───────────────────────────────
// AddWaha() selects the correct image tag automatically:
//   - NOWEB on Apple Silicon → devlikeapro/waha:noweb-arm
//   - NOWEB on x86_64        → devlikeapro/waha:noweb
// Switch engine via the WahaEngine enum (WEBJS, WPP, NOWEB, GOWS).
// WithPersistentLifetime() keeps the container alive across AppHost restarts (preserves QR auth).
// WithDataVolume() mounts waha-sessions:/app/.sessions to survive container recreation.
var waha = builder.AddWaha("waha", wahaApiKey, wahaDashboardPassword, wahaSwaggerPassword)
    .WithDataVolume()
    .WithPersistentLifetime();

var wahaEndpoint = waha.Resource.PrimaryEndpoint;

// ─── Webhook WebApi ───────────────────────────────────────────────────────────
// Dependency chain: waha → webhookApi → devTunnel (no circular deps)
// WaitFor(waha): ensures WAHA container is healthy before WebhookRegistrationService
// tries to register the webhook on startup.
// WEBHOOK_BASE_URL is the stable dev tunnel HTTPS URL — set once via user-secrets:
//   dotnet user-secrets set "WEBHOOK_BASE_URL" "https://xxx-7185.inc1.devtunnels.ms"
var webhookApi = builder.AddProject<Projects.Waha_WebApi>("webhook")
    .WithReference(wahaEndpoint)                  // WAHA service discovery → WahaApiClient base URL
    .WithEnvironment("WAHA_API_KEY", wahaApiKey)  // passed for HttpClient auth header
    .WithEnvironment("WEBHOOK_BASE_URL",           // tunnel URL for WebhookRegistrationService
        builder.Configuration["WEBHOOK_BASE_URL"] ?? string.Empty)
    .WaitFor(waha);                               // don't start until WAHA container is running

// ─── Dev Tunnel ───────────────────────────────────────────────────────────────
// Exposes WebApi publicly so WAHA (inside Docker) can POST webhook events to it.
// The tunnel URL is stable for 30 days (same tunnel ID reused across AppHost restarts).
// On first run: get the URL from the Aspire dashboard DevTunnelPort resource, then
//   dotnet user-secrets set "WEBHOOK_BASE_URL" "https://xxx-7185.inc1.devtunnels.ms"
// WebhookRegistrationService reads WEBHOOK_BASE_URL and registers with WAHA on startup.
var _ = builder.AddDevTunnel("waha-webhook")
    .WithReference(webhookApi)   // tunnel exposes webhookApi publicly
    .WithAnonymousAccess()       // WAHA must call webhook URL without authentication
    .WaitFor(webhookApi);        // don't expose tunnel until WebApi is ready

builder.Build().Run();