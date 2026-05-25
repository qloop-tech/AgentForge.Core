using AgentForge.Verticals.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Verticals.Travel;

public sealed class TravelVerticalPlugin : IVerticalPlugin
    , IVerticalDeploymentValidator
{
    public IVerticalDescriptor Descriptor { get; } = new TravelVerticalDescriptor();

    public IVerticalMcpRegistrar McpRegistrar { get; } = new TravelMcpRegistrar();

    public void RegisterWebApiServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IScheduledActionHandler, TravelScheduledActionHandler>();
    }

    public void ValidateDeployment() => TravelDataFiles.EnsureDataDirectoryExists();
}
