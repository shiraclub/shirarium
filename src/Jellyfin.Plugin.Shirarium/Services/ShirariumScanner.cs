using System.Collections;
using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Contracts;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

public sealed class ShirariumScanner
{
    private readonly ILibraryManager _libraryManager;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;
    private readonly PluginConfiguration? _configOverride;
    private readonly Func<string, CancellationToken, Task<ParseFilenameResponse?>>? _parseFilenameAsync;

    public ShirariumScanner(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger logger,
        PluginConfiguration? configOverride = null,
        Func<string, CancellationToken, Task<ParseFilenameResponse?>>? parseFilenameAsync = null)
    {
        _libraryManager = libraryManager;
        _applicationPaths = applicationPaths;
        _logger = logger;
        _configOverride = configOverride;
        _parseFilenameAsync = parseFilenameAsync;
    }

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
            items = EnumerateLibraryItems(_libraryManager.RootFolder).ToArray();
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

            examinedCount++;

            var reasons = ScanLogic.GetCandidateReasons(item);
            if (reasons.Length == 0)
            {
                continue;
            }

            candidateCount++;
            if (parsedCount >= maxItems)
            {
                skippedByLimitCount++;
                continue;
            }

            ParseFilenameResponse? parsed;
            if (_parseFilenameAsync is not null)
            {
                parsed = await _parseFilenameAsync(path, cancellationToken);
            }
            else
            {
                parsed = await engineClient!.ParseFilenameAsync(path, cancellationToken);
            }

            if (parsed is null)
            {
                engineFailureCount++;
                continue;
            }

            if (!ScanLogic.PassesConfidenceThreshold(parsed.Confidence, minConfidence))
            {
                skippedByConfidenceCount++;
                continue;
            }

            parsedCount++;

            var suggestion = new ScanSuggestion
            {
                ItemId = GetPropertyAsString(item, "Id"),
                Name = ScanLogic.GetStringProperty(item, "Name") ?? Path.GetFileNameWithoutExtension(path!),
                Path = path!,
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
            Suggestions = suggestions.ToArray()
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

    private static IEnumerable<object> EnumerateLibraryItems(object? rootFolder)
    {
        if (rootFolder is null)
        {
            yield break;
        }

        var type = rootFolder.GetType();
        var methods = type.GetMethods().Where(m => m.Name == "GetRecursiveChildren").ToArray();
        if (methods.Length == 0)
        {
            yield break;
        }

        foreach (var method in methods.OrderBy(m => m.GetParameters().Length))
        {
            var parameters = method.GetParameters();
            object? result;

            try
            {
                result = parameters.Length == 0
                    ? method.Invoke(rootFolder, null)
                    : method.Invoke(rootFolder, new object?[] { null });
            }
            catch
            {
                continue;
            }

            if (result is not IEnumerable enumerable)
            {
                continue;
            }

            foreach (var item in enumerable)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }

            yield break;
        }
    }

    private static string GetPropertyAsString(object item, string propertyName)
    {
        return ScanLogic.GetPropertyAsString(item, propertyName);
    }
}
