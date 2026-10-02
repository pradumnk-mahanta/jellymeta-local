using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JellyMetaLocal;

/// <summary>
/// Registers services for JellyMeta Local into the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<LocalMetadataMatcher>();
        serviceCollection.AddSingleton<NfoReader>();
    }
}

