using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for organization planning snapshots.
/// </summary>
public static class OrganizationPlanStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the organization plan file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute plan file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "organization-plan.json");
    }

    /// <summary>
    /// Reads the latest organization plan snapshot from disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>The stored plan snapshot, or an empty plan if not found or invalid.</returns>
    public static OrganizationPlanSnapshot Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        var snapshot = StoreFileJson.ReadOrDefault(filePath, JsonOptions, static () => new OrganizationPlanSnapshot());
        if (snapshot.SchemaVersion != SnapshotSchemaVersions.OrganizationPlan)
        {
            return new OrganizationPlanSnapshot();
        }

        snapshot.PlanFingerprint = PlanFingerprint.Compute(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Writes an organization plan snapshot to disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="snapshot">Plan snapshot to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync(
        IApplicationPaths applicationPaths,
        OrganizationPlanSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        snapshot = new OrganizationPlanSnapshot
        {
            SchemaVersion = SnapshotSchemaVersions.OrganizationPlan,
            GeneratedAtUtc = snapshot.GeneratedAtUtc,
            PlanFingerprint = snapshot.PlanFingerprint,
            RootPath = snapshot.RootPath,
            DryRunMode = snapshot.DryRunMode,
            SourceSuggestionCount = snapshot.SourceSuggestionCount,
            PlannedCount = snapshot.PlannedCount,
            NoopCount = snapshot.NoopCount,
            SkippedCount = snapshot.SkippedCount,
            ConflictCount = snapshot.ConflictCount,
            Entries = snapshot.Entries
        };
        snapshot.PlanFingerprint = PlanFingerprint.Compute(snapshot);
        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.WriteAsync(filePath, snapshot, JsonOptions, cancellationToken);
        await OrganizationPlanHistoryStore.AppendAsync(applicationPaths, snapshot, cancellationToken);
    }
}
