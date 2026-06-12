using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.NetflixRows.Api;
using Jellyfin.Plugin.NetflixRows.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.NetflixRows.Services
{
    /// <summary>
    /// Core service that translates a <see cref="RowDefinition"/> into a
    /// library query and returns the matching items.
    /// </summary>
    public class RowQueryService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IDtoService _dtoService;

        public RowQueryService(ILibraryManager libraryManager, IDtoService dtoService)
        {
            _libraryManager = libraryManager;
            _dtoService = dtoService;
        }

        /// <summary>
        /// Gets the configured library folders, for use in the admin configuration page.
        /// </summary>
        public List<LibrarySummaryDto> GetLibraries()
        {
            return _libraryManager.GetVirtualFolders()
                .Select(f => new LibrarySummaryDto
                {
                    Id = Guid.Parse(f.ItemId),
                    Name = f.Name
                })
                .ToList();
        }

        /// <summary>
        /// Gets all distinct genre names found across movies and series in the
        /// configured libraries (or all libraries if none configured).
        /// </summary>
        public List<string> GetAvailableGenres()
        {
            var config = Plugin.Instance!.Configuration;

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                Recursive = true,
                IsVirtualItem = false
            };

            ApplyLibraryScope(query, config);

            return _libraryManager.GetItemList(query)
                .SelectMany(item => item.Genres ?? Array.Empty<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Returns whether the given row currently has enough matching items to be shown.
        /// </summary>
        public bool RowHasEnoughItems(RowDefinition row, User? user)
        {
            return GetMatchingItems(row, user, row.MinItems).Count >= row.MinItems;
        }

        /// <summary>
        /// Gets the items for a row, formatted as DTOs for the API/web client.
        /// Returns an empty list if the row does not meet <see cref="RowDefinition.MinItems"/>.
        /// </summary>
        public List<BaseItemDto> GetItems(RowDefinition row, User? user)
        {
            var matched = GetMatchingItems(row, user, row.MinItems);

            if (matched.Count < row.MinItems)
            {
                return new List<BaseItemDto>();
            }

            var dtoOptions = new DtoOptions(true)
            {
                Fields = new[] { ItemFields.PrimaryImageAspectRatio }
            };

            return matched
                .Take(row.MaxItems)
                .Select(item => _dtoService.GetBaseItemDto(item, dtoOptions, user))
                .ToList();
        }

        private List<BaseItem> GetMatchingItems(RowDefinition row, User? user, int minRequired)
        {
            var config = Plugin.Instance!.Configuration;

            var itemTypes = row.MediaType switch
            {
                RowMediaType.Movies => new[] { BaseItemKind.Movie },
                RowMediaType.Series => new[] { BaseItemKind.Series },
                _ => new[] { BaseItemKind.Movie, BaseItemKind.Series }
            };

            var orderBy = row.SortOrder switch
            {
                RowSortOrder.Newest => new[] { (ItemSortBy.DateCreated, SortOrder.Descending) },
                RowSortOrder.Random => new[] { (ItemSortBy.Random, SortOrder.Ascending) },
                RowSortOrder.Rating => new[] { (ItemSortBy.CommunityRating, SortOrder.Descending) },
                _ => new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
            };

            var seen = new HashSet<Guid>();
            var matched = new List<BaseItem>();

            // Genres are matched with OR semantics: an item matches the row if it
            // has ANY of the configured genres. We query per-genre and merge,
            // because InternalItemsQuery.Genres requires ALL listed genres to match.
            var genres = row.Genres.Count > 0 ? row.Genres : new List<string> { null! };

            foreach (var genre in genres)
            {
                var query = new InternalItemsQuery(user)
                {
                    IncludeItemTypes = itemTypes,
                    Recursive = true,
                    IsVirtualItem = false,
                    OrderBy = orderBy,
                    Limit = row.MaxItems * 2
                };

                if (!string.IsNullOrEmpty(genre))
                {
                    query.Genres = new[] { genre };
                }

                ApplyLibraryScope(query, config);

                foreach (var item in _libraryManager.GetItemList(query))
                {
                    if (seen.Add(item.Id))
                    {
                        matched.Add(item);
                    }
                }

                // Early exit once we have plenty of items for max+min purposes.
                if (matched.Count >= row.MaxItems * 2)
                {
                    break;
                }
            }

            if (row.SortOrder == RowSortOrder.Random)
            {
                var rng = new Random();
                matched = matched.OrderBy(_ => rng.Next()).ToList();
            }

            return matched;
        }

        private static void ApplyLibraryScope(InternalItemsQuery query, PluginConfiguration config)
        {
            if (config.LibraryFolderIds.Count > 0)
            {
                query.AncestorIds = config.LibraryFolderIds.ToArray();
            }
        }
    }
}
