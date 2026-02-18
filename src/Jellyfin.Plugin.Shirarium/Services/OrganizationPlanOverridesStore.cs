using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for organization-plan review overrides.
/// </summary>
public static class OrganizationPlanOverridesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the override snapshot file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute override snapshot file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "organization-plan-overrides.json");
    }

    /// <summary>
    /// Reads the latest override snapshot from disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>The stored override snapshot, or an empty snapshot if not found or invalid.</returns>
    public static OrganizationPlanOverridesSnapshot Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        var snapshot = StoreFileJson.ReadOrDefault(filePath, JsonOptions, static () => new OrganizationPlanOverridesSnapshot());
        if (snapshot.SchemaVersion != SnapshotSchemaVersions.OrganizationPlanOverrides)
        {
            return new OrganizationPlanOverridesSnapshot();
        }

        return snapshot;
    }

    /// <summary>
    /// Reads overrides only if they match the provided plan fingerprint.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="planFingerprint">Target plan fingerprint.</param>
    /// <returns>Matching override snapshot, or an empty snapshot if mismatched.</returns>
    public static OrganizationPlanOverridesSnapshot ReadForFingerprint(
        IApplicationPaths applicationPaths,
        string planFingerprint)
    {
        var snapshot = Read(applicationPaths);
        if (string.IsNullOrWhiteSpace(planFingerprint)
            || !planFingerprint.Equals(snapshot.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new OrganizationPlanOverridesSnapshot
            {
                PlanFingerprint = planFingerprint ?? string.Empty
            };
        }

        return snapshot;
    }

    /// <summary>
    /// Writes an override snapshot to disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="snapshot">Override snapshot to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync(
        IApplicationPaths applicationPaths,
        OrganizationPlanOverridesSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var normalized = new OrganizationPlanOverridesSnapshot
        {
            SchemaVersion = SnapshotSchemaVersions.OrganizationPlanOverrides,
            PlanFingerprint = snapshot.PlanFingerprint,
            UpdatedAtUtc = snapshot.UpdatedAtUtc,
            Entries = snapshot.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.SourcePath))
                .OrderBy(entry => entry.SourcePath, PathComparison.Comparer)
                .ToArray()
        };

        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.WriteAsync(filePath, normalized, JsonOptions, cancellationToken);
        await OrganizationPlanOverridesHistoryStore.AppendAsync(applicationPaths, normalized, cancellationToken);
    }
}
