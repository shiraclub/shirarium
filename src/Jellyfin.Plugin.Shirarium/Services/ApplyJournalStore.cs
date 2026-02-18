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
        return StoreFileJson.ReadOrDefault(filePath, JsonOptions, static () => new ApplyJournalSnapshot());
    }

    /// <summary>
    /// Writes a full apply journal snapshot to disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="snapshot">Snapshot to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync(
        IApplicationPaths applicationPaths,
        ApplyJournalSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.WriteAsync(filePath, snapshot, JsonOptions, cancellationToken);
    }

    /// <summary>
    /// Appends one apply result to the audit journal.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="result">Apply result to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task AppendApplyAsync(
        IApplicationPaths applicationPaths,
        ApplyOrganizationPlanResult result,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.UpdateAsync(
            filePath,
            JsonOptions,
            static () => new ApplyJournalSnapshot(),
            snapshot =>
            {
                var runs = snapshot.Runs.ToList();
                runs.Add(result);
                return new ApplyJournalSnapshot
                {
                    Runs = runs.ToArray(),
                    UndoRuns = snapshot.UndoRuns
                };
            },
            cancellationToken);
    }

    /// <summary>
    /// Appends one undo result to the audit journal and marks the source apply run as restored.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="result">Undo result to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task AppendUndoAsync(
        IApplicationPaths applicationPaths,
        UndoApplyResult result,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.UpdateAsync(
            filePath,
            JsonOptions,
            static () => new ApplyJournalSnapshot(),
            snapshot =>
            {
                var runs = snapshot.Runs.ToArray();
                var runIndex = Array.FindIndex(runs, run => run.RunId.Equals(result.SourceApplyRunId, StringComparison.OrdinalIgnoreCase));
                if (runIndex >= 0)
                {
                    runs[runIndex].UndoneByRunId = result.UndoRunId;
                    runs[runIndex].UndoneAtUtc = result.UndoneAtUtc;
                }

                var undoRuns = snapshot.UndoRuns.ToList();
                undoRuns.Add(result);

                return new ApplyJournalSnapshot
                {
                    Runs = runs,
                    UndoRuns = undoRuns.ToArray()
                };
            },
            cancellationToken);
    }
}
