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
    private readonly ILibraryManager _libraryManager;
    private readonly PluginConfiguration? _configOverride;
    private readonly ISourceCandidateProvider _sourceCandidateProvider;
    private readonly EngineClient? _engineClient;
    private readonly Func<string, CancellationToken, Task<ParseFilenameResponse?>>? _parseFilenameAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShirariumScanner"/> class.
    /// </summary>
    public ShirariumScanner(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger logger,
        EngineClient engineClient,
        PluginConfiguration? configOverride = null)
        : this(
            applicationPaths,
            logger,
            libraryManager,
            new FilesystemCandidateProvider(libraryManager, logger),
            engineClient,
            configOverride)
    {
    }

    /// <summary>
    /// Internal constructor for testing with delegate override.
    /// </summary>
    internal ShirariumScanner(
        IApplicationPaths applicationPaths,
        ILogger logger,
        ISourceCandidateProvider sourceCandidateProvider,
        PluginConfiguration? configOverride,
        Func<string, CancellationToken, Task<ParseFilenameResponse?>> parseFilenameAsync)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _libraryManager = null!; // Not used in this test constructor flow or needs mock
        _sourceCandidateProvider = sourceCandidateProvider;
        _configOverride = configOverride;
        _parseFilenameAsync = parseFilenameAsync;
        _engineClient = null;
    }

    private ShirariumScanner(
        IApplicationPaths applicationPaths,
        ILogger logger,
        ILibraryManager libraryManager,
        ISourceCandidateProvider sourceCandidateProvider,
        EngineClient engineClient,
        PluginConfiguration? configOverride)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _libraryManager = libraryManager;
        _sourceCandidateProvider = sourceCandidateProvider;
        _engineClient = engineClient;
        _configOverride = configOverride;
        _parseFilenameAsync = null;
    }

    /// <summary>
    /// Executes a full dry-run scan and stores the resulting suggestion snapshot.
    /// </summary>
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
        var parseAttemptCount = 0;
        var skippedByLimitCount = 0;
        var skippedByConfidenceCount = 0;
        var engineFailureCount = 0;
        var candidateReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parserSourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var confidenceBucketCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> items;
        try
        {
            items = _sourceCandidateProvider.GetCandidates(cancellationToken).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shirarium failed to enumerate filesystem items.");
            return new ScanResultSnapshot
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                DryRunMode = config.DryRunMode
            };
        }

        foreach (var sourcePath in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ScanLogic.IsSupportedPath(sourcePath, extensions))
            {
                continue;
            }

            examinedCount++;

            // Cross-reference with Jellyfin
            object? jellyfinItem = null;
            var reasonsList = new List<string> { "Reorganization" };
            if (_libraryManager != null)
            {
                jellyfinItem = _libraryManager.FindByPath(sourcePath, false);
                if (jellyfinItem == null)
                {
                    reasonsList.Add("Unrecognized");
                }
                else if (!ScanLogic.HasAnyProviderIds(jellyfinItem))
                {
                    reasonsList.Add("MissingMetadata");
                }
            }
            var reasons = reasonsList.ToArray();

            candidateCount++;
            IncrementBuckets(candidateReasonCounts, reasons);
            
            if (parseAttemptCount >= maxItems)
            {
                skippedByLimitCount++;
                continue;
            }

            parseAttemptCount++;
            ParseFilenameResponse? parsed = null;
            
            if (_parseFilenameAsync != null)
            {
                parsed = await _parseFilenameAsync(sourcePath, cancellationToken);
            }
            else if (_engineClient != null)
            {
                parsed = await _engineClient.ParseFilenameAsync(sourcePath, cancellationToken);
            }
            else
            {
                _logger.LogError("ShirariumScanner misconfigured: No parser available.");
            }

            if (parsed is null)
            {
                engineFailureCount++;
                continue;
            }

            // If we found a matching Jellyfin item, prefer its metadata (Probe) over heuristics (Filename)
            if (jellyfinItem != null)
            {
                parsed = parsed with
                {
                    Resolution = ScanLogic.GetResolution(jellyfinItem) ?? parsed.Resolution,
                    VideoCodec = ScanLogic.GetVideoCodec(jellyfinItem) ?? parsed.VideoCodec,
                    AudioCodec = ScanLogic.GetAudioCodec(jellyfinItem) ?? parsed.AudioCodec,
                    AudioChannels = ScanLogic.GetAudioChannels(jellyfinItem) ?? parsed.AudioChannels,
                    MediaSource = ScanLogic.GetMediaSource(jellyfinItem) ?? parsed.MediaSource,
                    ReleaseGroup = ScanLogic.GetReleaseGroup(jellyfinItem) ?? parsed.ReleaseGroup,
                    Edition = ScanLogic.GetEdition(jellyfinItem) ?? parsed.Edition
                };
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
                ItemId = Guid.NewGuid().ToString("N"),
                Name = Path.GetFileNameWithoutExtension(sourcePath),
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
                ScannedAtUtc = DateTimeOffset.UtcNow,
                Resolution = parsed.Resolution,
                VideoCodec = parsed.VideoCodec,
                VideoBitDepth = null,
                AudioCodec = parsed.AudioCodec,
                AudioChannels = parsed.AudioChannels,
                ReleaseGroup = parsed.ReleaseGroup,
                MediaSource = parsed.MediaSource,
                Edition = parsed.Edition
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
