using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentForge.Verticals.Hosting;

public sealed class VerticalPluginBootstrapLogger(
    VerticalPluginBootstrapState bootstrapState,
    ILogger<VerticalPluginBootstrapLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (bootstrapState.IsLoaded)
        {
            logger.LogInformation("Vertical plugin loaded successfully.");
        }
        else
        {
            logger.LogError("Vertical plugin bootstrap failed. {Error}", bootstrapState.GetUnhealthyMessage());
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
