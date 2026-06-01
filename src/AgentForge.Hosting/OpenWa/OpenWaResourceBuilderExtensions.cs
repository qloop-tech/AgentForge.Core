using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding and configuring OpenWA resources in an Aspire AppHost.
/// </summary>
public static class OpenWaResourceBuilderExtensions
{
    private const string DefaultVersion = "0.1.6";
    private const string PrebuiltImageName = "ghcr.io/rmyndharis/openwa";

    private const string DataVolumeMountPath = "/app/data";
    private const string ApiContextPath = "../AgentForge.Hosting/OpenWa/Api";
    private const string DashboardContextPath = "../AgentForge.Hosting/OpenWa/Dashboard";

    /// <summary>
    /// Adds the OpenWA API container resource to the distributed application.
    /// </summary>
    public static IResourceBuilder<ContainerResource> AddOpenWa(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> apiKey,
        IResourceBuilder<ParameterResource> encryptionKey,
        string version = DefaultVersion,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(encryptionKey);

        var resourceBuilder = CreateOpenWaApiBuilder(builder, name, version)
            .WithHttpEndpoint(port: port, targetPort: 2785, name: "http")
            .WithEnvironment("NODE_ENV", "production")
            .WithEnvironment("PORT", "2785")
            .WithEnvironment("API_PREFIX", "/api")
            .WithEnvironment("API_MASTER_KEY", apiKey)
            .WithEnvironment("API_KEY_MASTER", apiKey)
            .WithEnvironment("ENCRYPTION_KEY", encryptionKey)
            .WithEnvironment("PUPPETEER_HEADLESS", "true")
            .WithEnvironment("PUPPETEER_ARGS", "--no-sandbox,--disable-setuid-sandbox,--disable-dev-shm-usage,--disable-gpu")
            .WithHttpHealthCheck("/api/health");

        resourceBuilder.WithUrl($"{resourceBuilder.Resource.GetEndpoint("http")}/api/docs", "OpenWA Swagger");

        return resourceBuilder;
    }

    private static IResourceBuilder<ContainerResource> CreateOpenWaApiBuilder(
        IDistributedApplicationBuilder builder,
        string name,
        string version)
    {
        var normalizedTag = NormalizeImageTag(version);
        if (normalizedTag is not null && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return builder.AddContainer(name, PrebuiltImageName, normalizedTag);
        }

        return builder
            .AddDockerfile(name, ApiContextPath)
            .WithBuildArg("OPENWA_VERSION", version);
    }

    /// <summary>
    /// Adds the OpenWA dashboard container built from the upstream dashboard source.
    /// </summary>
    public static IResourceBuilder<ContainerResource> AddOpenWaDashboard(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ContainerResource> openWa,
        string version = DefaultVersion,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(openWa);

        var resourceBuilder = builder
            .AddDockerfile(name, DashboardContextPath)
            .WithBuildArg("OPENWA_VERSION", version)
            .WithHttpEndpoint(port: port, targetPort: 80, name: "http")
            .WaitFor(openWa);

        var endpoint = resourceBuilder.Resource.GetEndpoint("http");
        resourceBuilder.WithUrl($"{endpoint}", "OpenWA Dashboard");

        return resourceBuilder;
    }

    private static string? NormalizeImageTag(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        if (version.Equals("main", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? version[1..]
            : version;
    }

    /// <summary>
    /// Persists OpenWA runtime data, including session auth state, across container restarts.
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithDataVolume(
        this IResourceBuilder<ContainerResource> builder,
        string volumeName = "openwa-data")
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithVolume(volumeName, DataVolumeMountPath);
    }

    /// <summary>
    /// Keeps the OpenWA container stable across AppHost restarts so the QR-authenticated session survives.
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithPersistentLifetime(
        this IResourceBuilder<ContainerResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithLifetime(ContainerLifetime.Persistent);
    }

    /// <summary>
    /// Configures OpenWA to use PostgreSQL for its provider-side persistence.
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithPostgres(
        this IResourceBuilder<ContainerResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(password);

        return builder
            .WithEnvironment("DATABASE_TYPE", "postgres")
            .WithEnvironment("POSTGRES_BUILTIN", "false")
            .WithEnvironment("DATABASE_HOST", database.Resource.Parent.Host)
            .WithEnvironment("DATABASE_PORT", database.Resource.Parent.Port)
            .WithEnvironment("DATABASE_NAME", database.Resource.DatabaseName)
            .WithEnvironment("DATABASE_USERNAME", database.Resource.Parent.UserNameReference)
            .WithEnvironment("DATABASE_PASSWORD", password);
    }

    /// <summary>
    /// Configures OpenWA to use Redis for cache and queue-related provider features.
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithRedis(
        this IResourceBuilder<ContainerResource> builder,
        IResourceBuilder<RedisResource> redis)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redis);

        return builder
            .WithEnvironment("CACHE_TYPE", "redis")
            .WithEnvironment("REDIS_ENABLED", "true")
            .WithEnvironment("REDIS_BUILTIN", "false")
            .WithEnvironment("REDIS_URL", redis.Resource.ConnectionStringExpression)
            .WithEnvironment("REDIS_HOST", redis.Resource.Host)
            .WithEnvironment("REDIS_PORT", redis.Resource.Port);
    }
}
