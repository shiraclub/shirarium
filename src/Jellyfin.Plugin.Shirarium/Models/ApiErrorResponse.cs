namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Machine-readable API error payload.
/// </summary>
public sealed class ApiErrorResponse
{
    /// <summary>
    /// Gets stable machine-readable error code.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets human-readable error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional additional error details.
    /// </summary>
    public object? Details { get; init; }
}
