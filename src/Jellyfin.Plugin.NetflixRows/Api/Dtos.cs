using System;

namespace Jellyfin.Plugin.NetflixRows.Api
{
    /// <summary>
    /// Lightweight summary of a configured row, used by the web client to know
    /// which rows exist before requesting their items.
    /// </summary>
    public class RowSummaryDto
    {
        /// <summary>
        /// Gets or sets the row id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the display title (including emoji), e.g. "🔥 Actie Films".
        /// </summary>
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>
    /// Summary of a Jellyfin library (CollectionFolder) used to populate the
    /// "Which libraries" picker on the configuration page.
    /// </summary>
    public class LibrarySummaryDto
    {
        /// <summary>
        /// Gets or sets the library id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the library display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
