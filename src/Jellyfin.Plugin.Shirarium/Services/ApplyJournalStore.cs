using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for apply audit journal snapshots.
/// </summary>
public static class ApplyJournalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the apply journal file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute apply journal file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "apply-journal.json");
    }

    /// <summary>
    /// Reads the apply journal snapshot from disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>The stored journal snapshot, or an empty snapshot if not found or invalid.</returns>
    public static ApplyJournalSnapshot Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        if (!File.Exists(filePath))
        {
            return new ApplyJournalSnapshot();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ApplyJournalSnapshot>(json, JsonOptions)
                ?? new ApplyJournalSnapshot();
        }
        catch
        {
            return new ApplyJournalSnapshot();
        }
    }

    /// <summary>
    /// Appends one apply result to the audit journal.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="result">Apply result to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task AppendAsync(
        IApplicationPaths applicationPaths,
        ApplyOrganizationPlanResult result,
        CancellationToken cancellationToken = default)
    {
        var snapshot = Read(applicationPaths);
        var runs = snapshot.Runs.ToList();
        runs.Add(result);

        var updated = new ApplyJournalSnapshot
        {
            Runs = runs.ToArray()
        };

        var filePath = GetFilePath(applicationPaths);
        var json = JsonSerializer.Serialize(updated, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
