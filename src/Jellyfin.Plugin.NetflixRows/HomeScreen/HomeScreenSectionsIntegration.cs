using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NetflixRows.HomeScreen
{
    /// <summary>
    /// OPTIONAL / EXPERIMENTAL integration with the community
    /// "Home Screen Sections" plugin (IAmParadox27/jellyfin-plugin-home-sections).
    ///
    /// That plugin allows other plugins to register native home-screen rows via
    /// <c>IHomeScreenManager.RegisterResultsDelegate(PluginDefinedSection)</c>, which
    /// renders directly inside the stock Jellyfin web client - no index.html patching
    /// needed.
    ///
    /// Because that plugin's internal API is not published as a stable contract and
    /// changes between versions, this class only performs a SAFE PROBE: it checks
    /// whether the plugin is loaded and logs guidance. The actual registration call
    /// is intentionally left as a documented extension point (see README "Plan A")
    /// so it can be wired up against the exact version installed on your server
    /// without risking a crash on servers that don't have it installed.
    ///
    /// If this integration is not present/working, the plugin still functions fully
    /// via the web injection fallback (see WebInjection/WebInjectionEntryPoint.cs).
    /// </summary>
    public class HomeScreenSectionsIntegration : IHostedService
    {
        private const string HomeScreenSectionsAssemblyName = "Jellyfin.Plugin.HomeScreenSections";

        private readonly ILogger<HomeScreenSectionsIntegration> _logger;

        public HomeScreenSectionsIntegration(ILogger<HomeScreenSectionsIntegration> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!Plugin.Instance!.Configuration.EnableHomeScreenSectionsIntegration)
            {
                return Task.CompletedTask;
            }

            try
            {
                var hssAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, HomeScreenSectionsAssemblyName, StringComparison.OrdinalIgnoreCase));

                if (hssAssembly is null)
                {
                    _logger.LogInformation(
                        "NetflixRows: 'Home Screen Sections' plugin not detected. " +
                        "Genre rows will be rendered via the web injection fallback only.");
                    return Task.CompletedTask;
                }

                _logger.LogInformation(
                    "NetflixRows: 'Home Screen Sections' plugin detected (assembly: {Assembly}). " +
                    "Native section registration is not wired up out of the box because its " +
                    "internal API differs between versions - see README section 'Plan A' for " +
                    "instructions on enabling native rows for your installed version. " +
                    "Falling back to web injection for now.",
                    hssAssembly.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NetflixRows: error while probing for 'Home Screen Sections' plugin.");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
