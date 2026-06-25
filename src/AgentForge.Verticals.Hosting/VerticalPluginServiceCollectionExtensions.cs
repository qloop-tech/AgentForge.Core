using AgentForge.Verticals.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentForge.Verticals.Hosting;

public static class VerticalPluginServiceCollectionExtensions
{
    public static IServiceCollection AddVerticalPluginBootstrap(
        this IServiceCollection services,
        VerticalPluginBootstrapState bootstrapState)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(bootstrapState);

        services.AddSingleton(bootstrapState);
        services.AddHostedService<VerticalPluginBootstrapLogger>();
        services.AddSingleton(sp => sp.GetRequiredService<VerticalPluginBootstrapState>().GetRequiredPlugin());
        services.AddSingleton<IVerticalDescriptor>(sp =>
            sp.GetRequiredService<VerticalPluginBootstrapState>().GetRequiredPlugin().CreateDescriptor(sp));
        services.AddHealthChecks()
            .AddCheck<VerticalPluginHealthCheck>(
                "vertical-plugin",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }
}
