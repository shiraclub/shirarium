using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for immutable review lock snapshots.
/// </summary>
public static class ReviewLockStore
{
    private const int MaxEntries = 200;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the review lock file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute review lock file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "review-locks.json");
    }

    /// <summary>
    /// Reads all persisted review locks from disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Persisted locks ordered by write sequence.</returns>
    public static ReviewLockSnapshot[] Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ReviewLockSnapshot[]>(json, JsonOptions)
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Reads one review lock by immutable id.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="reviewId">Review lock id.</param>
    /// <returns>Matching lock when found; otherwise <see langword="null"/>.</returns>
    public static ReviewLockSnapshot? ReadById(IApplicationPaths applicationPaths, string reviewId)
    {
        if (string.IsNullOrWhiteSpace(reviewId))
        {
            return null;
        }

        return Read(applicationPaths)
            .FirstOrDefault(lockSnapshot => lockSnapshot.ReviewId.Equals(reviewId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Appends one immutable review lock with bounded retention.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="snapshot">Review lock snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task AppendAsync(
        IApplicationPaths applicationPaths,
        ReviewLockSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var entries = Read(applicationPaths).ToList();
        if (entries.Any(existing => existing.ReviewId.Equals(snapshot.ReviewId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("DuplicateReviewId");
        }

        entries.Add(snapshot);
        if (entries.Count > MaxEntries)
        {
            entries = entries.Skip(entries.Count - MaxEntries).ToList();
        }

        await WriteAsync(applicationPaths, entries, cancellationToken);
    }

    /// <summary>
    /// Marks one review lock as consumed by an apply run.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="reviewId">Review lock id.</param>
    /// <param name="runId">Apply run id.</param>
    /// <param name="appliedAtUtc">Apply timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task MarkAppliedAsync(
        IApplicationPaths applicationPaths,
        string reviewId,
        string runId,
        DateTimeOffset appliedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var entries = Read(applicationPaths).ToList();
        var index = entries.FindIndex(existing => existing.ReviewId.Equals(reviewId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("ReviewLockNotFound");
        }

        entries[index].AppliedRunId = runId;
        entries[index].AppliedAtUtc = appliedAtUtc;
        await WriteAsync(applicationPaths, entries, cancellationToken);
    }

    private static async Task WriteAsync(
        IApplicationPaths applicationPaths,
        IReadOnlyCollection<ReviewLockSnapshot> entries,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(applicationPaths);
        var json = JsonSerializer.Serialize(entries.ToArray(), JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
