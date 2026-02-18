using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium;

/// <summary>
/// Handles plugin startup and shutdown logic.
/// </summary>
public sealed class PluginEntryPoint : IEntryPoint
{
    private readonly InferenceManager _inferenceManager;
    private readonly ILogger<PluginEntryPoint> _logger;

    public PluginEntryPoint(Plugin plugin, ILogger<PluginEntryPoint> logger)
    {
        _logger = logger;
        _inferenceManager = new InferenceManager(plugin.AppPaths, logger);
    }

    /// <inheritdoc />
    public Task RunAsync()
    {
        _logger.LogInformation("Shirarium plugin entry point starting...");
        
        // Ensure inference engine is ready in the background to not block server startup
        _ = Task.Run(async () => 
        {
            try 
            {
                await _inferenceManager.EnsureReadyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing local inference engine.");
            }
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _inferenceManager.Dispose();
    }
}
