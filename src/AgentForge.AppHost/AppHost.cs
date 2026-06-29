using AgentForge.AppHost;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker;
using Projects;

#pragma warning disable ASPIREJAVASCRIPT001

var builder = DistributedApplication.CreateBuilder(args);
var settings = AppHostSettings.Load(builder.Configuration, builder.ExecutionContext.IsPublishMode);
var localParameters = AppHostLocalParameters.Create(builder, settings.IsPublishMode);

#region Secrets
var openWaApiKey = builder.AddParameter("openWaApiKey", secret: true);
var openWaEncryptionKey = builder.AddParameter("openWaEncryptionKey", secret: true);
var openWaWebhookSecret = builder.AddParameter("openWaWebhookSecret", secret: true);
var openWaPostgresPassword = builder.AddParameter("openWaPostgresPassword", secret: true);
#endregion

#region Core resources

var openWaRedis = builder.AddRedis("openwa-redis")
    .WithDataVolume()
    .WithRedisInsight();

var openWaPostgres = builder.AddPostgres("openwa-postgres", password: openWaPostgresPassword)
    .WithEnvironment("POSTGRES_DB", "openwa")
    .WithDataVolume()
    .WithPgWeb();
var openWaDatabase = openWaPostgres.AddDatabase("openwa-db", "openwa");
var openWaNodeEnvironment = settings.IsPublishMode ? "production" : "development";
var openWaDataPath = settings.IsPublishMode ? "/app/data" : "data";
var openWaPuppeteerExecutablePath = settings.IsPublishMode
    ? "/usr/bin/chromium"
    : builder.Configuration["OPENWA_PUPPETEER_EXECUTABLE_PATH"]
      ?? "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
var openWaRedisEndpoint = settings.IsPublishMode
    ? openWaRedis.Resource.PrimaryEndpoint
    : openWaRedis.Resource.GetEndpoint("secondary");

var openWa = builder.AddNodeApp("openwa", "../OpenWA", "dist/main")
    .WithNpm(installCommand: "ci", installArgs: ["--ignore-scripts"])
    .WithRunScript("start:dev")
    .WithBuildScript("build")
    .WithHttpEndpoint(port: 2785, env: "PORT")
    .WithEnvironment("NODE_ENV", openWaNodeEnvironment)
    .WithEnvironment("API_PREFIX", "/api")
    .WithEnvironment("API_MASTER_KEY", openWaApiKey)
    .WithEnvironment("API_KEY_MASTER", openWaApiKey)
    .WithEnvironment("ENCRYPTION_KEY", openWaEncryptionKey)
    .WithEnvironment("DATABASE_HOST", openWaDatabase.Resource.Parent.Host)
    .WithEnvironment("DATABASE_PORT", openWaDatabase.Resource.Parent.Port)
    .WithEnvironment("DATABASE_NAME", openWaDatabase.Resource.DatabaseName)
    .WithEnvironment("DATABASE_USERNAME", openWaDatabase.Resource.Parent.UserNameReference)
    .WithEnvironment("DATABASE_PASSWORD", openWaPostgresPassword)
    .WithEnvironment("REDIS_HOST", openWaRedisEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("REDIS_PORT", openWaRedisEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("REDIS_PASSWORD", openWaRedis.Resource.PasswordParameter!)
    .WithEnvironment("REDIS_TLS", "false")
    .WithEnvironment("SESSION_DATA_PATH", $"{openWaDataPath}/sessions")
    .WithEnvironment("STORAGE_TYPE", "local")
    .WithEnvironment("STORAGE_LOCAL_PATH", $"{openWaDataPath}/media")
    .WithEnvironment("PUPPETEER_HEADLESS", "true")
    .WithEnvironment("PUPPETEER_EXECUTABLE_PATH", openWaPuppeteerExecutablePath)
    .WithEnvironment("PUPPETEER_ARGS", "--no-sandbox,--disable-setuid-sandbox,--disable-dev-shm-usage,--disable-gpu")
    .WithEnvironment("SOCKET_IO_PATH", "/api/socket.io")
    .WithEnvironment("AUTO_START_SESSIONS", "true")
    .WithHttpHealthCheck("/api/health")
    .WaitFor(openWaPostgres)
    .WaitFor(openWaRedis);
openWa.WithUrl($"{openWa.Resource.GetEndpoint("http")}/api/docs", "OpenWA Swagger");

var openWaDashboard = builder.AddViteApp("openwa-dashboard", "../OpenWA/dashboard")
    .WithEnvironment("OPENWA_HTTP", openWa.GetEndpoint("http"))
    .WithEnvironment("VITE_SOCKET_IO_PATH", "/api/socket.io")
    .WaitFor(openWa);
openWaDashboard.PublishAsStaticWebsite("/api", openWa, options =>
{
    options.OutputPath = "dist";
    options.StripPrefix = false;
});

var openWaEndpoint = openWa.Resource.GetEndpoint("http");

var mcpServer = builder.AddProject<AgentForge_McpHost>("mcpserver")
    .WithHttpHealthCheck("/health")
    .WithLocalVerticalInputs(localParameters)
    .WithPublishVerticalRuntime(settings, "mcpserver");

var aiFoundry = builder.AddConnectionString("ai-foundry");

var webhookApi = builder.AddProject<AgentForge_WebApi>("webhook")
    .WithHttpHealthCheck("/health")
    .WithReference(openWaEndpoint)
    .WithReference(mcpServer)
    .WithReference(aiFoundry)
    .WithReference(openWaRedis)
    .WithEnvironment("OPENWA_API_KEY", openWaApiKey)
    .WithEnvironment("OPENWA_WEBHOOK_SECRET", openWaWebhookSecret)
    .WaitFor(openWa)
    .WaitFor(openWaRedis)
    .WaitFor(mcpServer)
    .WithLocalVerticalInputs(localParameters);
webhookApi.WithPublishVerticalRuntime(settings, "webhook");
#endregion

#region Local-only tooling
if (!settings.IsPublishMode)
{
    builder.AddMcpInspector("mcp-inspector", options =>
    {
        options.InspectorVersion = "0.17.5";
    })
        .WithMcpServer(mcpServer);

    var webhookTunnel = builder.AddDevTunnel("openwa-webhook")
        .WithReference(webhookApi)
        .WithAnonymousAccess();

    webhookApi.WithReference(webhookApi, webhookTunnel);
}
#endregion

#region Publish-only resources
if (settings.IsPublishMode)
{
    openWa.PublishAsDockerFile(container =>
    {
        container
            .WithDockerfileFactory("../OpenWA", _ => """
                FROM node:22-bookworm-slim AS build
                WORKDIR /app
                ENV PUPPETEER_SKIP_DOWNLOAD=true
                COPY package*.json ./
                RUN npm ci --ignore-scripts
                COPY . .
                RUN npm run build
                RUN npm prune --omit=dev --ignore-scripts

                FROM node:22-bookworm-slim AS runtime
                RUN apt-get update \
                    && apt-get install -y --no-install-recommends \
                        chromium \
                        dumb-init \
                        ca-certificates \
                        fonts-liberation \
                        libasound2 \
                        libatk-bridge2.0-0 \
                        libatk1.0-0 \
                        libcups2 \
                        libdrm2 \
                        libgbm1 \
                        libgtk-3-0 \
                        libnspr4 \
                        libnss3 \
                        libxcomposite1 \
                        libxdamage1 \
                        libxrandr2 \
                        xdg-utils \
                    && rm -rf /var/lib/apt/lists/*
                ENV NODE_ENV=production
                ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
                WORKDIR /app
                COPY --from=build /app/package*.json ./
                COPY --from=build /app/node_modules ./node_modules
                COPY --from=build /app/dist ./dist
                EXPOSE 2785
                ENTRYPOINT ["dumb-init", "--"]
                CMD ["node", "dist/main"]
                """)
            .WithVolume("openwa-data", "/app/data");
    });

    openWaDashboard.PublishAsDockerComposeService((_, service) =>
    {
        service.Ports.Add("${OPENWA_DASHBOARD_HOST_PORT:-2886}:5000");
    });

    var composeDashboardBrowserToken = builder.AddParameter("composeDashboardBrowserToken", secret: true);

    builder.AddDockerComposeEnvironment("compose")
        .ConfigureEnvFile(env =>
        {
            SetEnvMetadata(
                env,
                "COMPOSEDASHBOARDBROWSERTOKEN",
                defaultValue: null,
                description: "Browser token used to sign in to the published Aspire dashboard.");
            SetEnvMetadata(
                env,
                "WEBHOOK_BASE_URL",
                settings.ConfiguredWebhookBaseUrl,
                "Public base URL that OpenWA should call for webhook delivery in published deployments. " +
                "For local demos, set this to your external tunnel URL before starting Docker Compose.");
            SetEnvMetadata(
                env,
                "WEBHOOK_HOST_PORT",
                "8080",
                "Host port that exposes the published webhook container for direct VPS access or an external tunnel.");
            SetEnvMetadata(
                env,
                "OPENWA_DASHBOARD_HOST_PORT",
                "2886",
                "Host port that exposes the published OpenWA dashboard for VPS setup, QR scanning, or an external tunnel.");
        })
        .WithDashboard(dashboard => dashboard
            .WithEnvironment("Dashboard__ApplicationName", "AgentForge")
            .WithEnvironment("Dashboard__Frontend__AuthMode", "BrowserToken")
            .WithEnvironment("Dashboard__Frontend__BrowserToken", composeDashboardBrowserToken));
}
#endregion

builder.Build().Run();
return;

static void SetEnvMetadata(
    IDictionary<string, CapturedEnvironmentVariable> env,
    string name,
    string? defaultValue,
    string description)
{
    if (!env.TryGetValue(name, out var variable))
    {
        variable = new CapturedEnvironmentVariable
        {
            Name = name
        };
        env[name] = variable;
    }

    variable.Description = description;

    if (defaultValue is not null)
    {
        variable.DefaultValue = defaultValue;
    }
}
