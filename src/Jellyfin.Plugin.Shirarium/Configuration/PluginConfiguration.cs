using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Shirarium.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public string EngineBaseUrl { get; set; } = "http://engine:8787";

    public bool EnableAiParsing { get; set; } = true;

    public bool DryRunMode { get; set; } = true;

    public bool EnablePostScanTask { get; set; } = true;

    public int MaxItemsPerRun { get; set; } = 500;

    public double MinConfidence { get; set; } = 0.55;

    public string[] ScanFileExtensions { get; set; } =
    [
        ".mkv",
        ".mp4",
        ".avi",
        ".mov",
        ".wmv",
        ".m4v",
        ".ts",
        ".m2ts",
        ".webm"
    ];
}
