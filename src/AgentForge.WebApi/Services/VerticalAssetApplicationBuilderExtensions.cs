using AgentForge.Verticals.Abstractions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace AgentForge.WebApi.Services;

public static class VerticalAssetApplicationBuilderExtensions
{
    public static IApplicationBuilder UseVerticalAssets(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var descriptor = app.ApplicationServices.GetRequiredService<IVerticalDescriptor>();
        var prefix = NormalizeRequestPath(descriptor.AssetPathPrefix);
        if (prefix is null || string.IsNullOrWhiteSpace(descriptor.AssetRootPath))
        {
            return app;
        }

        var physicalRoot = Path.Combine(descriptor.AssetRootPath, prefix.TrimStart('/'));
        if (!Directory.Exists(physicalRoot))
        {
            throw new DirectoryNotFoundException(
                $"Configured asset path '{prefix}' for vertical '{descriptor.VerticalId}' was not found under '{descriptor.AssetRootPath}'.");
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(physicalRoot),
            RequestPath = prefix,
            ContentTypeProvider = new FileExtensionContentTypeProvider()
        });

        return app;
    }

    private static string? NormalizeRequestPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return "/" + path.Trim('/');
    }
}
