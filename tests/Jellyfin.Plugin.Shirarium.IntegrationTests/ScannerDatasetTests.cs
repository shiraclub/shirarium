using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Contracts;
using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.IntegrationTests;

public sealed class ScannerDatasetTests
{
    [Fact]
    public async Task RunAsync_WithSyntheticDataset_ComputesExpectedCountersAndBuckets()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var runtime = ScannerDatasetHarness.BuildRuntime(
                root,
                [
                    new ScannerDatasetHarness.SyntheticScannerItemSpec
                    {
                        RelativePath = "incoming/Movie.One.2001.1080p.mkv",
                        HasProviderIds = false,
                        ParseResponse = CreateParseResponse("Movie One", "movie", 2001, null, null, 0.91, "heuristic")
                    },
                    new ScannerDatasetHarness.SyntheticScannerItemSpec
                    {
                        RelativePath = "incoming/Show.S01E01.mkv",
                        HasProviderIds = false,
                        Overview = "Episode one",
                        ParseResponse = CreateParseResponse("Show", "episode", null, 1, 1, 0.40, "heuristic")
                    },
                    new ScannerDatasetHarness.SyntheticScannerItemSpec
                    {
                        RelativePath = "incoming/Show.S01E02.mkv",
                        HasProviderIds = false,
                        ParseResponse = null
                    },
                    new ScannerDatasetHarness.SyntheticScannerItemSpec
                    {
                        RelativePath = "incoming/Already.Tagged.mp4",
                        HasProviderIds = true,
                        Overview = "Tagged",
                        ProductionYear = 2024,
                        ParseResponse = CreateParseResponse("Already Tagged", "movie", 2024, null, null, 0.99, "heuristic")
                    },
                    new ScannerDatasetHarness.SyntheticScannerItemSpec
                    {
                        RelativePath = "incoming/Second.Movie.2002.mkv",
                        HasProviderIds = false,
                        ParseResponse = CreateParseResponse("Second Movie", "movie", 2002, null, null, 0.82, "heuristic")
                    },
                    new ScannerDatasetHarness.SyntheticScannerItemSpec
                    {
                        RelativePath = "incoming/Third.Movie.2003.mkv",
                        HasProviderIds = false,
                        ParseResponse = CreateParseResponse("Third Movie", "movie", 2003, null, null, 0.83, "heuristic")
                    },
                    new ScannerDatasetHarness.SyntheticScannerItemSpec
                    {
                        RelativePath = "incoming/ignored.txt",
                        HasProviderIds = false,
                        ParseResponse = CreateParseResponse("Ignored", "unknown", null, null, null, 0.22, "heuristic")
                    }
                ]);

            var scanner = new ShirariumScanner(
                applicationPaths,
                NullLogger.Instance,
                runtime.Provider,
                new PluginConfiguration
                {
                    EnableAiParsing = true,
                    DryRunMode = true,
                    MaxItemsPerRun = 2,
                    MinConfidence = 0.55
                },
                runtime.ParseFilenameAsync);

            var snapshot = await scanner.RunAsync();

            Assert.Equal(6, snapshot.ExaminedCount);
            Assert.Equal(6, snapshot.CandidateCount);
            Assert.Equal(1, snapshot.ParsedCount);
            Assert.Equal(4, snapshot.SkippedByLimitCount);
            Assert.Equal(1, snapshot.SkippedByConfidenceCount);
            Assert.Equal(0, snapshot.EngineFailureCount);

            Assert.Single(snapshot.Suggestions);
            Assert.Contains(snapshot.Suggestions, suggestion => suggestion.SuggestedTitle == "Movie One");

            AssertBucket(snapshot.CandidateReasonCounts, "MissingProviderIds", 5);
            AssertBucket(snapshot.CandidateReasonCounts, "MissingOverview", 4);
            AssertBucket(snapshot.CandidateReasonCounts, "MissingProductionYear", 5);
            AssertBucket(snapshot.CandidateReasonCounts, "ReorganizationAudit", 1);

            AssertBucket(snapshot.ParserSourceCounts, "heuristic", 2);
            AssertBucket(snapshot.ConfidenceBucketCounts, "0.4-0.5", 1);
            AssertBucket(snapshot.ConfidenceBucketCounts, "0.9-1.0", 1);

            var invocationCounts = runtime.ParseInvocationCounts;
            Assert.Equal(2, invocationCounts.Values.Sum());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunAsync_WhenSourceProviderThrows_ReturnsEmptySnapshot()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var scanner = new ShirariumScanner(
                applicationPaths,
                NullLogger.Instance,
                new ThrowingSourceCandidateProvider(),
                new PluginConfiguration
                {
                    EnableAiParsing = true,
                    DryRunMode = true
                },
                (_, _) => Task.FromResult<ParseFilenameResponse?>(null));

            var snapshot = await scanner.RunAsync();

            Assert.Empty(snapshot.Suggestions);
            Assert.Equal(0, snapshot.ExaminedCount);
            Assert.Equal(0, snapshot.CandidateCount);
            Assert.Equal(0, snapshot.ParsedCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static ParseFilenameResponse CreateParseResponse(
        string title,
        string mediaType,
        int? year,
        int? season,
        int? episode,
        double confidence,
        string source)
    {
        return new ParseFilenameResponse
        {
            Title = title,
            MediaType = mediaType,
            Year = year,
            Season = season,
            Episode = episode,
            Confidence = confidence,
            Source = source,
            RawTokens = []
        };
    }

    private static void AssertBucket(
        IEnumerable<ScanCountBucket> buckets,
        string key,
        int expectedCount)
    {
        var bucket = Assert.Single(buckets, value => value.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedCount, bucket.Count);
    }

    private static TestApplicationPaths CreateApplicationPaths(string root)
    {
        var dataPath = Path.Combine(root, "jellyfin-data");
        Directory.CreateDirectory(dataPath);

        return new TestApplicationPaths
        {
            DataPath = dataPath,
            ProgramDataPath = dataPath,
            ProgramSystemPath = dataPath,
            CachePath = Path.Combine(root, "cache"),
            TempDirectory = Path.Combine(root, "tmp"),
            PluginsPath = Path.Combine(dataPath, "plugins"),
            BackupPath = Path.Combine(root, "backup"),
            VirtualDataPath = dataPath,
            LogDirectoryPath = Path.Combine(root, "logs"),
            ConfigurationDirectoryPath = Path.Combine(dataPath, "config"),
            SystemConfigurationFilePath = Path.Combine(dataPath, "system.xml"),
            WebPath = Path.Combine(root, "web"),
            PluginConfigurationsPath = Path.Combine(dataPath, "plugin-configs"),
            ImageCachePath = Path.Combine(root, "image-cache"),
            TrickplayPath = Path.Combine(root, "trickplay")
        };
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "shirarium-scanner-datasets", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class ThrowingSourceCandidateProvider : ISourceCandidateProvider
    {
        public IEnumerable<string> GetCandidates(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Synthetic source failure.");
        }
    }
}
