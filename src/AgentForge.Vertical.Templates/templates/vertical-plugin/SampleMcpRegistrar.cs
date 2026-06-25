using System.Reflection;
using AgentForge.Verticals.Abstractions;
using AgentForge.Vertical.Template.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Vertical.Template;

public sealed class SampleMcpRegistrar : IVerticalMcpRegistrar
{
    public Assembly McpAssembly => typeof(SampleMcpRegistrar).Assembly;

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<SampleCatalogService>();
    }
}
