using Jellyfin.Plugin.NetflixRows.HomeScreen;
using Jellyfin.Plugin.NetflixRows.Services;
using Jellyfin.Plugin.NetflixRows.WebInjection;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.NetflixRows
{
    /// <summary>
    /// Registers plugin services into Jellyfin's dependency injection container.
    /// Discovered automatically by Jellyfin on startup.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<RowQueryService>();

            // Plan A: best-effort native integration with the community
            // "Home Screen Sections" plugin, if it is installed.
            serviceCollection.AddHostedService<HomeScreenSectionsIntegration>();

            // Plan B: inject a small script/stylesheet into the web client
            // so genre rows are rendered even without Home Screen Sections.
            serviceCollection.AddHostedService<WebInjectionEntryPoint>();
        }
    }
}
