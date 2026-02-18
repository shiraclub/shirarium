using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for dry-run suggestion snapshots.
/// </summary>
public static class SuggestionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the snapshot file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute snapshot file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "dryrun-suggestions.json");
    }

    /// <summary>
    /// Reads the latest snapshot from disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>The stored snapshot, or an empty snapshot if not found or invalid.</returns>
    public static ScanResultSnapshot Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        return StoreFileJson.ReadOrDefault(filePath, JsonOptions, static () => new ScanResultSnapshot());
    }

    /// <summary>
    /// Writes a snapshot to disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="snapshot">Snapshot to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync(
        IApplicationPaths applicationPaths,
        ScanResultSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.WriteAsync(filePath, snapshot, JsonOptions, cancellationToken);
    }
}
