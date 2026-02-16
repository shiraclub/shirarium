using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Api;

/// <summary>
/// Administrative endpoints for triggering scans and reading suggestion snapshots.
/// </summary>
[ApiController]
[Route("Shirarium")]
public sealed class ShirariumController : ControllerBase
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ShirariumScanner _scanner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShirariumController"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    public ShirariumController(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger<ShirariumController> logger)
    {
        _applicationPaths = applicationPaths;
        _scanner = new ShirariumScanner(libraryManager, applicationPaths, logger);
    }

    /// <summary>
    /// Gets the latest stored dry-run suggestion snapshot.
    /// </summary>
    /// <returns>The current suggestion snapshot.</returns>
    [HttpGet("suggestions")]
    public ActionResult<ScanResultSnapshot> GetSuggestions()
    {
        return Ok(SuggestionStore.Read(_applicationPaths));
    }

    /// <summary>
    /// Triggers a dry-run scan and returns the generated snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated scan snapshot.</returns>
    [HttpPost("scan")]
    public async Task<ActionResult<ScanResultSnapshot>> RunScan(CancellationToken cancellationToken)
    {
        var snapshot = await _scanner.RunAsync(cancellationToken);
        return Ok(snapshot);
    }
}
