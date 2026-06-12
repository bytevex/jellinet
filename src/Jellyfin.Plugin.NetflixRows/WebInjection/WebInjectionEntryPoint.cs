using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NetflixRows.WebInjection
{
    /// <summary>
    /// Plan B / fallback: patches the Jellyfin web client's index.html on startup to
    /// load a small script and stylesheet that render the configured genre rows
    /// directly on the home page using the plugin's REST API.
    ///
    /// This is the same general technique used by other Jellyfin plugins that ship
    /// custom web UI (e.g. skip-intro buttons, custom branding). It is reversible:
    /// re-running the official Jellyfin installer/update will overwrite index.html
    /// and remove the injection, after which it is re-applied automatically the
    /// next time the server starts with this plugin enabled.
    /// </summary>
    public class WebInjectionEntryPoint : IHostedService
    {
        private const string MarkerComment = "<!-- NetflixRows:start -->";
        private const string MarkerEnd = "<!-- NetflixRows:end -->";

        private readonly IApplicationPaths _appPaths;
        private readonly ILogger<WebInjectionEntryPoint> _logger;

        public WebInjectionEntryPoint(IApplicationPaths appPaths, ILogger<WebInjectionEntryPoint> logger)
        {
            _appPaths = appPaths;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!Plugin.Instance!.Configuration.EnableWebInjection)
            {
                return Task.CompletedTask;
            }

            try
            {
                PatchIndexHtml();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NetflixRows: failed to inject web client assets into index.html.");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void PatchIndexHtml()
        {
            var indexPath = Path.Combine(_appPaths.WebPath, "index.html");

            if (!File.Exists(indexPath))
            {
                _logger.LogWarning("NetflixRows: index.html not found at '{Path}'. Skipping web injection.", indexPath);
                return;
            }

            var contents = File.ReadAllText(indexPath);

            if (contents.Contains(MarkerComment, StringComparison.Ordinal))
            {
                // Already injected (e.g. after a restart). Nothing to do.
                return;
            }

            const string injection =
                MarkerComment +
                "<link rel=\"stylesheet\" href=\"/NetflixRows/web/netflixRows.css\">" +
                "<script defer src=\"/NetflixRows/web/netflixRows.js\"></script>" +
                MarkerEnd;

            if (!contents.Contains("</body>", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("NetflixRows: could not find </body> tag in index.html. Skipping web injection.");
                return;
            }

            var patched = ReplaceLastOccurrence(contents, "</body>", injection + "</body>");

            // Keep a copy of the original so it can be restored manually if needed.
            var backupPath = indexPath + ".netflixrows.bak";
            if (!File.Exists(backupPath))
            {
                File.WriteAllText(backupPath, contents);
            }

            File.WriteAllText(indexPath, patched);
            _logger.LogInformation("NetflixRows: injected genre row script into {Path}.", indexPath);
        }

        private static string ReplaceLastOccurrence(string source, string find, string replace)
        {
            var index = source.LastIndexOf(find, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return source;
            }

            return source[..index] + replace + source[(index + find.Length)..];
        }
    }
}
