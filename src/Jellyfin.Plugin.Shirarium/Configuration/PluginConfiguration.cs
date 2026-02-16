using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Shirarium.Configuration;

/// <summary>
/// Configurable settings for the Shirarium plugin.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the base URL of the Shirarium engine service.
    /// </summary>
    public string EngineBaseUrl { get; set; } = "http://engine:8787";

    /// <summary>
    /// Gets or sets a value indicating whether filename parsing via engine is enabled.
    /// </summary>
    public bool EnableAiParsing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether scan behavior should remain non-destructive.
    /// </summary>
    public bool DryRunMode { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the post-library-scan task should run automatically.
    /// </summary>
    public bool EnablePostScanTask { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of candidate items to parse per run.
    /// </summary>
    public int MaxItemsPerRun { get; set; } = 500;

    /// <summary>
    /// Gets or sets the minimum confidence required for a suggestion to be stored.
    /// </summary>
    public double MinConfidence { get; set; } = 0.55;

    /// <summary>
    /// Gets or sets the file extensions considered for scan candidates.
    /// </summary>
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

    /// <summary>
    /// Gets or sets a value indicating whether physical file organization planning is enabled.
    /// </summary>
    public bool EnableFileOrganizationPlanning { get; set; } = true;

    /// <summary>
    /// Gets or sets the destination root path used when generating organization plans.
    /// </summary>
    public string OrganizationRootPath { get; set; } = "/media";

    /// <summary>
    /// Gets or sets a value indicating whether generated path segments should be sanitized.
    /// </summary>
    public bool NormalizePathSegments { get; set; } = true;
}
