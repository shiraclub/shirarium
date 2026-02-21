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
    [Obsolete("Use ExternalOllamaUrl or EnableManagedLocalInference instead.")]
    public string EngineBaseUrl { get; set; } = "http://engine:8787";

    /// <summary>
    /// Gets or sets the external Ollama (or OpenAI compatible) base URL.
    /// </summary>
    public string ExternalOllamaUrl { get; set; } = string.Empty;

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
    /// Gets or sets a value indicating whether to use a managed local inference engine (llama.cpp) 
    /// managed directly by the plugin instead of an external Ollama/vLLM instance.
    /// </summary>
    public bool EnableManagedLocalInference { get; set; } = true;

    /// <summary>
    /// Gets or sets the port number used by the managed local inference engine.
    /// </summary>
    public int InferencePort { get; set; } = 11434;

    /// <summary>
    /// Gets or sets a value indicating whether the plugin should automatically download 
    /// the recommended LLM model if it is missing.
    /// </summary>
    public bool AutoDownloadModel { get; set; } = true;

    /// <summary>
    /// Gets or sets the local path to the GGUF model file. 
    /// If empty, defaults to the plugin data directory.
    /// </summary>
    public string LocalModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL used to download the recommended model (Qwen 2.5 3B GGUF).
    /// </summary>
    public string ModelUrl { get; set; } = "https://huggingface.co/Qwen/Qwen2.5-3B-Instruct-GGUF/resolve/main/qwen2.5-3b-instruct-q4_k_m.gguf";

    /// <summary>
    /// Gets or sets the destination root path used when generating organization plans.
    /// </summary>
    public string OrganizationRootPath { get; set; } = "/media";

    /// <summary>
    /// Gets or sets a value indicating whether generated path segments should be sanitized.
    /// </summary>
    public bool NormalizePathSegments { get; set; } = true;

    /// <summary>
    /// Gets or sets the relative movie path template under <see cref="OrganizationRootPath"/>.
    /// </summary>
    /// <remarks>
    /// Available tokens: {Title}, {TitleWithYear}, {Year}, {Resolution}, {VideoCodec}, {VideoBitDepth}, {AudioCodec}, {AudioChannels}, {ReleaseGroup}, {MediaSource}, {Edition}.
    /// </remarks>
    public string MoviePathTemplate { get; set; } = "{TitleWithYear} [{Resolution}]/{TitleWithYear} [{Resolution}]";

    /// <summary>
    /// Gets or sets the relative episode path template under <see cref="OrganizationRootPath"/>.
    /// </summary>
    /// <remarks>
    /// Available tokens: {Title}, {Season}, {Season2}, {Episode}, {Episode2}, {Resolution}, {VideoCodec}, {VideoBitDepth}, {AudioCodec}, {AudioChannels}, {ReleaseGroup}, {MediaSource}, {Edition}.
    /// </remarks>
    public string EpisodePathTemplate { get; set; } = "{Title}/Season {Season2}/{Title} S{Season2}E{Episode2} [{Resolution}]";

    /// <summary>
    /// Gets or sets the plan-time target conflict policy.
    /// </summary>
    /// <remarks>
    /// Valid values: 
    /// <list type="bullet">
    ///   <item><description><c>fail</c>: Mark duplicate targets as conflicts.</description></item>
    ///   <item><description><c>skip</c>: Skip items that would result in a duplicate target.</description></item>
    ///   <item><description><c>suffix</c>: Append a numeric suffix (e.g., " (2)") to the target filename.</description></item>
    /// </list>
    /// </remarks>
    public string TargetConflictPolicy { get; set; } = "fail";
}
