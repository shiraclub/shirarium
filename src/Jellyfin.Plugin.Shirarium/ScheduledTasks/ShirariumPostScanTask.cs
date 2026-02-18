using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.ScheduledTasks;

/// <summary>
/// Post-scan task that runs Shirarium candidate parsing and organization planning after library scans.
/// </summary>
public sealed class ShirariumPostScanTask : ILibraryPostScanTask
{
    private readonly OrganizationPlanner _planner;
    private readonly ShirariumScanner _scanner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShirariumPostScanTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="loggerFactory">Logger factory for creating service loggers.</param>
    public ShirariumPostScanTask(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger<ShirariumPostScanTask> logger,
        ILoggerFactory loggerFactory)
    {
        // Manual Composition Root for Plugin Services
        // In a future refactor, we could move this to a proper DI registration if Jellyfin supports it better.
        var heuristicParser = new HeuristicParser();
        var ollamaService = new OllamaService(loggerFactory.CreateLogger<OllamaService>());
        var engineClient = new EngineClient(heuristicParser, ollamaService);

        _scanner = new ShirariumScanner(
            libraryManager,
            applicationPaths,
            loggerFactory.CreateLogger<ShirariumScanner>(), // Use dedicated logger for scanner
            engineClient);
            
        _planner = new OrganizationPlanner(applicationPaths, logger);
    }

    /// <summary>
    /// Executes the post-scan task.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null || !plugin.Configuration.EnablePostScanTask)
        {
            progress.Report(100);
            return;
        }

        progress.Report(10);
        var snapshot = await _scanner.RunAsync(cancellationToken);

        progress.Report(70);
        await _planner.RunAsync(snapshot, cancellationToken);

        progress.Report(100);
    }
}
