using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Builds and persists non-destructive physical organization plans from scan suggestions.
/// </summary>
public sealed class OrganizationPlanner
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;
    private readonly PluginConfiguration? _configOverride;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationPlanner"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="configOverride">Optional configuration override used mainly for tests.</param>
    public OrganizationPlanner(
        IApplicationPaths applicationPaths,
        ILogger logger,
        PluginConfiguration? configOverride = null)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _configOverride = configOverride;
    }

    /// <summary>
    /// Builds and stores an organization plan from either the supplied scan snapshot or the latest stored one.
    /// </summary>
    /// <param name="sourceSnapshot">Optional scan snapshot to plan from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated organization plan snapshot.</returns>
    public async Task<OrganizationPlanSnapshot> RunAsync(
        ScanResultSnapshot? sourceSnapshot = null,
        CancellationToken cancellationToken = default)
    {
        var plugin = Plugin.Instance;
        var config = _configOverride ?? plugin?.Configuration;
        if (config is null || !config.EnableFileOrganizationPlanning)
        {
            return new OrganizationPlanSnapshot
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                DryRunMode = config?.DryRunMode ?? true,
                RootPath = config?.OrganizationRootPath ?? string.Empty
            };
        }

        var scanSnapshot = sourceSnapshot ?? SuggestionStore.Read(_applicationPaths);
        var plan = BuildPlan(scanSnapshot, config);

        await OrganizationPlanStore.WriteAsync(_applicationPaths, plan, cancellationToken);

        _logger.LogInformation(
            "Shirarium organization plan complete. SourceSuggestions={SourceSuggestions} Planned={Planned} Noop={Noop} Skipped={Skipped} Conflicts={Conflicts}",
            plan.SourceSuggestionCount,
            plan.PlannedCount,
            plan.NoopCount,
            plan.SkippedCount,
            plan.ConflictCount);

        return plan;
    }

    internal static OrganizationPlanSnapshot BuildPlan(
        ScanResultSnapshot sourceSnapshot,
        PluginConfiguration config)
    {
        var entries = sourceSnapshot.Suggestions
            .Select(suggestion => OrganizationPlanLogic.BuildEntry(
                suggestion,
                config.OrganizationRootPath,
                config.NormalizePathSegments))
            .ToList();

        OrganizationPlanLogic.MarkDuplicateTargetConflicts(entries);

        return new OrganizationPlanSnapshot
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            RootPath = config.OrganizationRootPath,
            DryRunMode = config.DryRunMode,
            SourceSuggestionCount = entries.Count,
            PlannedCount = entries.Count(entry => entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase)),
            NoopCount = entries.Count(entry => entry.Action.Equals("none", StringComparison.OrdinalIgnoreCase)),
            SkippedCount = entries.Count(entry => entry.Action.Equals("skip", StringComparison.OrdinalIgnoreCase)),
            ConflictCount = entries.Count(entry => entry.Action.Equals("conflict", StringComparison.OrdinalIgnoreCase)),
            Entries = entries.ToArray()
        };
    }
}
