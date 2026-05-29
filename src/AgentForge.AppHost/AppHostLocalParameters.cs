using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace AgentForge.AppHost;

internal sealed class AppHostLocalParameters(
    IResourceBuilder<ParameterResource> verticalPluginPath,
    IResourceBuilder<ParameterResource> customerConfigPath)
{
    public IResourceBuilder<ParameterResource> VerticalPluginPath { get; } = verticalPluginPath;

    public IResourceBuilder<ParameterResource> CustomerConfigPath { get; } = customerConfigPath;

    public static AppHostLocalParameters? Create(IDistributedApplicationBuilder builder, bool isPublishMode)
    {
        if (isPublishMode)
        {
            return null;
        }

        var verticalPluginPath = builder.AddParameterFromConfiguration("vertical-plugin-path", "VERTICAL_PLUGIN_PATH")
            .WithDescription("Optional local path to an external vertical plugin folder or DLL. Leave blank to use the built-in in-tree vertical.");
        var customerConfigPath = builder.AddParameterFromConfiguration("customer-config-path", "CUSTOMER_CONFIG_PATH")
            .WithDescription("Optional local path to a customer config folder containing customer-profile.json and an optional prompt.md.");

        return new AppHostLocalParameters(verticalPluginPath, customerConfigPath);
    }
}
