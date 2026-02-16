using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Contracts;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Performs candidate discovery and dry-run parsing for Jellyfin library items.
/// </summary>
public sealed class ShirariumScanner
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;
    private readonly PluginConfiguration? _configOverride;
    private readonly ISourceCandidateProvider _sourceCandidateProvider;
    private readonly Func<string, CancellationToken, Task<ParseFilenameResponse?>>? _parseFilenameAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShirariumScanner"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="configOverride">Optional configuration override used mainly for tests.</param>
    /// <param name="parseFilenameAsync">Optional parser delegate used mainly for tests.</param>
    public ShirariumScanner(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger logger,
        PluginConfiguration? configOverride = null,
        Func<string, CancellationToken, Task<ParseFilenameResponse?>>? parseFilenameAsync = null)
        : this(
            applicationPaths,
            logger,
            new JellyfinLibraryCandidateProvider(libraryManager),
            configOverride,
            parseFilenameAsync)
    {
    }

    internal ShirariumScanner(
        IApplicationPaths applicationPaths,
        ILogger logger,
        ISourceCandidateProvider sourceCandidateProvider,
        PluginConfiguration? configOverride = null,
        Func<string, CancellationToken, Task<ParseFilenameResponse?>>? parseFilenameAsync = null)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _configOverride = configOverride;
        _parseFilenameAsync = parseFilenameAsync;
        _sourceCandidateProvider = sourceCandidateProvider;
    }

    /// <summary>
    /// Executes a full dry-run scan and stores the resulting suggestion snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated scan snapshot.</returns>
    public async Task<ScanResultSnapshot> RunAsync(CancellationToken cancellationToken = default)
    {
        var plugin = Plugin.Instance;
        var config = _configOverride ?? plugin?.Configuration;
        if (config is null || !config.EnableAiParsing)
        {
            return new ScanResultSnapshot
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                DryRunMode = config?.DryRunMode ?? true
            };
        }

        var extensions = ScanLogic.BuildExtensionSet(config.ScanFileExtensions);
        var maxItems = Math.Max(1, config.MaxItemsPerRun);
        var minConfidence = Math.Clamp(config.MinConfidence, 0.0, 1.0);

        var suggestions = new List<ScanSuggestion>();
        var examinedCount = 0;
        var candidateCount = 0;
        var parsedCount = 0;
        var skippedByLimitCount = 0;
        var skippedByConfidenceCount = 0;
        var engineFailureCount = 0;
        var candidateReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parserSourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var confidenceBucketCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        HttpClient? httpClient = null;
        EngineClient? engineClient = null;
        if (_parseFilenameAsync is null)
        {
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            engineClient = new EngineClient(httpClient);
        }

        IEnumerable<object> items;
        try
        {
            items = _sourceCandidateProvider.GetCandidates(cancellationToken).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shirarium failed to enumerate Jellyfin library items.");
            return new ScanResultSnapshot
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                DryRunMode = config.DryRunMode
            };
        }

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = ScanLogic.GetStringProperty(item, "Path");
            if (!ScanLogic.IsSupportedPath(path, extensions))
            {
                continue;
            }

            var sourcePath = path!;

            examinedCount++;

            var reasons = ScanLogic.GetCandidateReasons(item);
            if (reasons.Length == 0)
            {
                continue;
            }

            candidateCount++;
            IncrementBuckets(candidateReasonCounts, reasons);
            if (parsedCount >= maxItems)
            {
                skippedByLimitCount++;
                continue;
            }

            ParseFilenameResponse? parsed;
            if (_parseFilenameAsync is not null)
            {
                parsed = await _parseFilenameAsync(sourcePath, cancellationToken);
            }
            else
            {
                parsed = await engineClient!.ParseFilenameAsync(sourcePath, cancellationToken);
            }

            if (parsed is null)
            {
                engineFailureCount++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(parsed.Source))
            {
                IncrementBucket(parserSourceCounts, parsed.Source);
            }

            IncrementBucket(confidenceBucketCounts, GetConfidenceBucketKey(parsed.Confidence));

            if (!ScanLogic.PassesConfidenceThreshold(parsed.Confidence, minConfidence))
            {
                skippedByConfidenceCount++;
                continue;
            }

            parsedCount++;

            var suggestion = new ScanSuggestion
            {
                ItemId = GetPropertyAsString(item, "Id"),
                Name = ScanLogic.GetStringProperty(item, "Name") ?? Path.GetFileNameWithoutExtension(sourcePath),
                Path = sourcePath,
                SuggestedTitle = parsed.Title,
                SuggestedMediaType = parsed.MediaType,
                SuggestedYear = parsed.Year,
                SuggestedSeason = parsed.Season,
                SuggestedEpisode = parsed.Episode,
                Confidence = parsed.Confidence,
                Source = parsed.Source,
                CandidateReasons = reasons,
                RawTokens = parsed.RawTokens.ToArray(),
                ScannedAtUtc = DateTimeOffset.UtcNow
            };

            suggestions.Add(suggestion);
        }

        var snapshot = new ScanResultSnapshot
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            DryRunMode = config.DryRunMode,
            ExaminedCount = examinedCount,
            CandidateCount = candidateCount,
            ParsedCount = parsedCount,
            SkippedByLimitCount = skippedByLimitCount,
            SkippedByConfidenceCount = skippedByConfidenceCount,
            EngineFailureCount = engineFailureCount,
            Suggestions = suggestions.ToArray(),
            CandidateReasonCounts = BuildBuckets(candidateReasonCounts),
            ParserSourceCounts = BuildBuckets(parserSourceCounts),
            ConfidenceBucketCounts = BuildBuckets(confidenceBucketCounts)
        };

        await SuggestionStore.WriteAsync(_applicationPaths, snapshot, cancellationToken);

        _logger.LogInformation(
            "Shirarium dry-run complete. Examined={Examined} Candidates={Candidates} Parsed={Parsed} SkippedLimit={SkippedLimit} SkippedConfidence={SkippedConfidence} EngineFailures={EngineFailures}",
            examinedCount,
            candidateCount,
            parsedCount,
            skippedByLimitCount,
            skippedByConfidenceCount,
            engineFailureCount);

        httpClient?.Dispose();

        return snapshot;
    }

    private static string GetPropertyAsString(object item, string propertyName)
    {
        return ScanLogic.GetPropertyAsString(item, propertyName);
    }

    private static void IncrementBuckets(
        Dictionary<string, int> buckets,
        IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            IncrementBucket(buckets, key);
        }
    }

    private static void IncrementBucket(
        Dictionary<string, int> buckets,
        string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalizedKey = key.Trim();
        buckets.TryGetValue(normalizedKey, out var existingCount);
        buckets[normalizedKey] = existingCount + 1;
    }

    private static ScanCountBucket[] BuildBuckets(Dictionary<string, int> buckets)
    {
        return buckets
            .Select(pair => new ScanCountBucket
            {
                Key = pair.Key,
                Count = pair.Value
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetConfidenceBucketKey(double confidence)
    {
        var clamped = Math.Clamp(confidence, 0.0, 1.0);
        if (clamped >= 1.0)
        {
            return "1.0";
        }

        var lower = Math.Floor(clamped * 10) / 10.0;
        var upper = lower + 0.1;
        return $"{lower:0.0}-{upper:0.0}";
    }
}
