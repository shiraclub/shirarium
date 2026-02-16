namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Schema version constants for persisted Shirarium snapshot payloads.
/// </summary>
public static class SnapshotSchemaVersions
{
    /// <summary>
    /// Gets organization-plan snapshot schema version.
    /// </summary>
    public const int OrganizationPlan = 1;

    /// <summary>
    /// Gets organization-plan overrides snapshot schema version.
    /// </summary>
    public const int OrganizationPlanOverrides = 1;

    /// <summary>
    /// Gets review-lock snapshot schema version.
    /// </summary>
    public const int ReviewLock = 1;

    /// <summary>
    /// Gets reviewed-preflight token snapshot schema version.
    /// </summary>
    public const int ReviewedPreflight = 1;
}
