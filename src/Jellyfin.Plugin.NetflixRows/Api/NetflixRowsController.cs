using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Jellyfin.Plugin.NetflixRows.Configuration;
using Jellyfin.Plugin.NetflixRows.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.NetflixRows.Api
{
    /// <summary>
    /// REST API surface for the Netflix Rows plugin.
    ///
    /// - GET /NetflixRows/Rows            -> list of enabled rows (id + title)
    /// - GET /NetflixRows/Rows/{id}/Items -> items for a row, for the current user
    /// - GET /NetflixRows/Genres          -> distinct genres found in the library
    /// - GET /NetflixRows/Libraries       -> available library folders (for config page)
    /// - GET /NetflixRows/web/{file}      -> static assets for the web injection script
    /// </summary>
    [ApiController]
    [Route("NetflixRows")]
    public class NetflixRowsController : ControllerBase
    {
        private readonly RowQueryService _rowQueryService;
        private readonly IUserManager _userManager;

        public NetflixRowsController(RowQueryService rowQueryService, IUserManager userManager)
        {
            _rowQueryService = rowQueryService;
            _userManager = userManager;
        }

        /// <summary>
        /// Returns the list of enabled rows that currently have enough items to display.
        /// </summary>
        [HttpGet("Rows")]
        [Authorize]
        public ActionResult<IEnumerable<RowSummaryDto>> GetRows()
        {
            var config = Plugin.Instance!.Configuration;
            var user = GetCurrentUser();

            var rows = config.Rows
                .Where(r => r.Enabled)
                .Where(r => _rowQueryService.RowHasEnoughItems(r, user))
                .Select(r => new RowSummaryDto { Id = r.Id, Title = r.Title });

            return Ok(rows);
        }

        /// <summary>
        /// Returns the items for a given row, for the current user.
        /// </summary>
        [HttpGet("Rows/{rowId}/Items")]
        [Authorize]
        public ActionResult<IEnumerable<BaseItemDto>> GetRowItems([FromRoute] Guid rowId)
        {
            var config = Plugin.Instance!.Configuration;
            var row = config.Rows.FirstOrDefault(r => r.Id == rowId && r.Enabled);
            if (row is null)
            {
                return NotFound();
            }

            var user = GetCurrentUser();
            return Ok(_rowQueryService.GetItems(row, user));
        }

        /// <summary>
        /// Returns all distinct genre names found in the configured libraries.
        /// Used by the admin configuration page to suggest genre values.
        /// </summary>
        [HttpGet("Genres")]
        [Authorize(Policy = "RequiresElevation")]
        public ActionResult<IEnumerable<string>> GetGenres()
        {
            return Ok(_rowQueryService.GetAvailableGenres());
        }

        /// <summary>
        /// Returns the available library folders, for the admin configuration page.
        /// </summary>
        [HttpGet("Libraries")]
        [Authorize(Policy = "RequiresElevation")]
        public ActionResult<IEnumerable<LibrarySummaryDto>> GetLibraries()
        {
            return Ok(_rowQueryService.GetLibraries());
        }

        /// <summary>
        /// Serves the small static JS/CSS assets used by the web injection fallback.
        /// Anonymous access is required because the script is loaded before login.
        /// </summary>
        [HttpGet("web/{fileName}")]
        [AllowAnonymous]
        public ActionResult GetWebAsset([FromRoute] string fileName)
        {
            var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["netflixRows.js"] = "application/javascript",
                ["netflixRows.css"] = "text/css"
            };

            if (!allowed.TryGetValue(fileName, out var contentType))
            {
                return NotFound();
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{typeof(Plugin).Namespace}.WebInjection.wwwroot.{fileName}";
            var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                return NotFound();
            }

            return File(stream, contentType);
        }

        private Jellyfin.Database.Implementations.Entities.User? GetCurrentUser()
        {
            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            return _userManager.GetUserById(userId);
        }
    }
}
