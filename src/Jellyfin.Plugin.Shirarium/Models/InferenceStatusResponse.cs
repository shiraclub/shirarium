namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Status of the managed local inference engine.
/// </summary>
public sealed class InferenceStatusResponse
{
    /// <summary>
    /// Gets the current status (e.g., Idle, DownloadingModel, Ready, Error).
    /// </summary>
    public string Status { get; init; } = "Idle";

    /// <summary>
    /// Gets the download progress percentage (0-100).
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Gets the error message if status is Error.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the model being used.
    /// </summary>
    public string ModelName { get; init; } = "qwen3-4b-instruct";

    /// <summary>
    /// Gets the port the inference server is listening on.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Gets additional technical metadata from the LLM.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
