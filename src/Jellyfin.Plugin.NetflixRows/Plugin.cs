using System;
using System.Collections.Generic;
using Jellyfin.Plugin.NetflixRows.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.NetflixRows
{
    /// <summary>
    /// Main plugin entry point. Holds the static <see cref="Instance"/> reference
    /// used by the API controller and background services to read configuration.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "Netflix Rows";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("a1b2c3d4-e5f6-4789-a012-b3c4d5e6f7a8");

        /// <inheritdoc />
        public override string Description =>
            "Genereert automatisch Netflix-stijl genre-rijen voor films en series op basis van genre metadata.";

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "NetflixRows",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
                },
                new PluginPageInfo
                {
                    Name = "NetflixRowsConfigPage.js",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.js"
                }
            };
        }
    }
}
