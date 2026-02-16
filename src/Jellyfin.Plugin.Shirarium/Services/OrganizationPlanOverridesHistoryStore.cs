using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for organization-plan override revision history.
/// </summary>
public static class OrganizationPlanOverridesHistoryStore
{
    private const int MaxEntries = 200;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the override history file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute override history file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "organization-plan-overrides-history.json");
    }

    /// <summary>
    /// Reads override history entries from disk.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Persisted override history entries ordered by write sequence.</returns>
    public static OrganizationPlanOverridesSnapshot[] Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var entries = JsonSerializer.Deserialize<OrganizationPlanOverridesSnapshot[]>(json, JsonOptions)
                ?? [];
            return entries
                .Where(snapshot => snapshot.SchemaVersion == SnapshotSchemaVersions.OrganizationPlanOverrides)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Appends one override revision to history with bounded retention.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="snapshot">Override snapshot to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task AppendAsync(
        IApplicationPaths applicationPaths,
        OrganizationPlanOverridesSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        snapshot = new OrganizationPlanOverridesSnapshot
        {
            SchemaVersion = SnapshotSchemaVersions.OrganizationPlanOverrides,
            PlanFingerprint = snapshot.PlanFingerprint,
            UpdatedAtUtc = snapshot.UpdatedAtUtc,
            Entries = snapshot.Entries
        };
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
