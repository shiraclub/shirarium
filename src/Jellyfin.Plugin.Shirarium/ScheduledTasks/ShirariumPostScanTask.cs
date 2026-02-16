using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.ScheduledTasks;

public sealed class ShirariumPostScanTask : ILibraryPostScanTask
{
    private readonly ShirariumScanner _scanner;

    public ShirariumPostScanTask(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger<ShirariumPostScanTask> logger)
    {
        _scanner = new ShirariumScanner(libraryManager, applicationPaths, logger);
    }

    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null || !plugin.Configuration.EnablePostScanTask)
        {
            progress.Report(100);
            return;
        }

        progress.Report(10);
        await _scanner.RunAsync(cancellationToken);
        progress.Report(100);
    }
}
