using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for organization plan revision history.
/// </summary>
public static class OrganizationPlanHistoryStore
{
    private const int MaxEntries = 200;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the plan history file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute plan history file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "organization-plan-history.json");
    }

    /// <summary>
    /// Reads the plan history from disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Persisted plan history entries ordered by write sequence.</returns>
    public static OrganizationPlanSnapshot[] Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<OrganizationPlanSnapshot[]>(json, JsonOptions)
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Appends one plan revision to history with bounded retention.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="snapshot">Plan snapshot to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task AppendAsync(
        IApplicationPaths applicationPaths,
        OrganizationPlanSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var entries = Read(applicationPaths).ToList();
        entries.Add(snapshot);

        if (entries.Count > MaxEntries)
        {
            entries = entries.Skip(entries.Count - MaxEntries).ToList();
        }

        var filePath = GetFilePath(applicationPaths);
        var json = JsonSerializer.Serialize(entries.ToArray(), JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
