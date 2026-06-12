using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.NetflixRows.Configuration
{
    /// <summary>
    /// Which library item types a row should include.
    /// </summary>
    public enum RowMediaType
    {
        Movies = 0,
        Series = 1,
        Both = 2
    }

    /// <summary>
    /// Sort order applied to the items within a row.
    /// </summary>
    public enum RowSortOrder
    {
        Newest = 0,
        Random = 1,
        Rating = 2,
        Title = 3
    }

    /// <summary>
    /// Configuration for a single Netflix-style genre row.
    /// </summary>
    public class RowDefinition
    {
        /// <summary>
        /// Gets or sets the unique identifier of this row. Generated once and kept stable
        /// so the web client / Home Screen Sections can reference it.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the display title of the row, e.g. "🔥 Actie Films".
        /// </summary>
        public string Title { get; set; } = "New row";

        /// <summary>
        /// Gets or sets the genres that an item must match (matches any of the listed genres).
        /// </summary>
        public List<string> Genres { get; set; } = new();

        /// <summary>
        /// Gets or sets which media types this row should include.
        /// </summary>
        public RowMediaType MediaType { get; set; } = RowMediaType.Both;

        /// <summary>
        /// Gets or sets the sort order of items within the row.
        /// </summary>
        public RowSortOrder SortOrder { get; set; } = RowSortOrder.Newest;

        /// <summary>
        /// Gets or sets the minimum number of matching items required for the row to be shown at all.
        /// </summary>
        public int MinItems { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum number of items returned for the row.
        /// </summary>
        public int MaxItems { get; set; } = 24;

        /// <summary>
        /// Gets or sets a value indicating whether this row is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Plugin configuration, edited via the admin dashboard configuration page.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the library folder ids (CollectionFolder ids) that rows should be
        /// restricted to. Empty = search all libraries.
        /// </summary>
        public List<Guid> LibraryFolderIds { get; set; } = new();

        /// <summary>
        /// Gets or sets the configured genre rows.
        /// </summary>
        public List<RowDefinition> Rows { get; set; } = new()
        {
            new RowDefinition { Title = "🔥 Actie Films", Genres = new List<string> { "Action" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "😂 Komedie Films", Genres = new List<string> { "Comedy" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "👻 Horror Films", Genres = new List<string> { "Horror" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "🚀 Sci-Fi Films", Genres = new List<string> { "Science Fiction" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "🗺️ Avontuur Films", Genres = new List<string> { "Adventure" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "🐉 Fantasy Films", Genres = new List<string> { "Fantasy" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "💖 Romantiek Films", Genres = new List<string> { "Romance" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "🎬 Animatiefilms", Genres = new List<string> { "Animation" }, MediaType = RowMediaType.Movies },
            new RowDefinition { Title = "🎭 Drama Series", Genres = new List<string> { "Drama" }, MediaType = RowMediaType.Series },
            new RowDefinition { Title = "🕵️ Crime Series", Genres = new List<string> { "Crime" }, MediaType = RowMediaType.Series },
            new RowDefinition { Title = "🔎 Mysterie Series", Genres = new List<string> { "Mystery" }, MediaType = RowMediaType.Series },
            new RowDefinition { Title = "👻 Horror Series", Genres = new List<string> { "Horror" }, MediaType = RowMediaType.Series },
            new RowDefinition { Title = "🚀 Sci-Fi Series", Genres = new List<string> { "Science Fiction" }, MediaType = RowMediaType.Series },
            new RowDefinition { Title = "👨‍👩‍👧 Familie Series", Genres = new List<string> { "Family" }, MediaType = RowMediaType.Series }
        };

        /// <summary>
        /// Gets or sets a value indicating whether the plugin should try to register
        /// rows with the community "Home Screen Sections" plugin, if installed.
        /// </summary>
        public bool EnableHomeScreenSectionsIntegration { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the plugin should inject its
        /// fallback script/stylesheet into the Jellyfin web client's index.html.
        /// </summary>
        public bool EnableWebInjection { get; set; } = true;

        /// <summary>
        /// Gets or sets a list of home screen section titles (as displayed on the
        /// home page, e.g. "Onlangs toegevoegde films") that should be hidden by
        /// the web injection script. Matching is case-insensitive and ignores
        /// leading/trailing whitespace.
        /// </summary>
        public List<string> HiddenHomeSections { get; set; } = new();
    }
}
