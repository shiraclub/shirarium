using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Api;

[ApiController]
[Route("Shirarium")]
public sealed class ShirariumController : ControllerBase
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ShirariumScanner _scanner;

    public ShirariumController(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger<ShirariumController> logger)
    {
        _applicationPaths = applicationPaths;
        _scanner = new ShirariumScanner(libraryManager, applicationPaths, logger);
    }

    [HttpGet("suggestions")]
    public ActionResult<ScanResultSnapshot> GetSuggestions()
    {
        return Ok(SuggestionStore.Read(_applicationPaths));
    }

    [HttpPost("scan")]
    public async Task<ActionResult<ScanResultSnapshot>> RunScan(CancellationToken cancellationToken)
    {
        var snapshot = await _scanner.RunAsync(cancellationToken);
        return Ok(snapshot);
    }
}
