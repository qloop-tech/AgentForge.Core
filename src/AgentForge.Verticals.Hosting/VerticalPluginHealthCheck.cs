using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentForge.Verticals.Hosting;

public sealed class VerticalPluginHealthCheck(
    VerticalPluginBootstrapState bootstrapState,
    IServiceProvider serviceProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (bootstrapState.TryValidateReady(serviceProvider, out var error))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Vertical plugin loaded."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(error ?? "Vertical plugin is unavailable."));
    }
}
