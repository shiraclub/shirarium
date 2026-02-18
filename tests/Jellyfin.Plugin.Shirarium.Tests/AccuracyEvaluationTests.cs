using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;
using Xunit.Abstractions;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class AccuracyEvaluationTests
{
    private readonly ITestOutputHelper _output;

    public AccuracyEvaluationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EvaluateTierAGoldenStandard_ComputesAccuracyScore()
    {
        var manifestPath = ResolveRepoFilePath("datasets", "regression", "tier-a-golden.json");
        var json = File.ReadAllText(manifestPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var entries = root.GetProperty("entries");

        int total = 0;
        int passed = 0;
        var failures = new List<string>();

        var parser = new HeuristicParser();

        _output.WriteLine($"Evaluating manifest: {root.GetProperty("name").GetString()}");
        _output.WriteLine($"Description: {root.GetProperty("description").GetString()}");
        _output.WriteLine("--------------------------------------------------");

        foreach (var entry in entries.EnumerateArray())
        {
            total++;
            var relativePath = entry.GetProperty("relativePath").GetString()!;
            var expected = entry.GetProperty("expected");
            var expectedTitle = expected.TryGetProperty("title", out var t) ? t.GetString() : null;
            var expectedYear = expected.TryGetProperty("year", out var y) ? (int?)y.GetInt32() : null;
            var expectedSeason = expected.TryGetProperty("season", out var s) ? (int?)s.GetInt32() : null;
            var expectedEpisode = expected.TryGetProperty("episode", out var e) ? (int?)e.GetInt32() : null;
            var expectedMediaType = expected.GetProperty("mediaType").GetString();

            var result = parser.Parse(relativePath);

            bool isMatch = true;
            if (!string.Equals(result.MediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase)) isMatch = false;
            if (expectedYear.HasValue && result.Year != expectedYear.Value) isMatch = false;
            if (expectedSeason.HasValue && result.Season != expectedSeason.Value) isMatch = false;
            if (expectedEpisode.HasValue && result.Episode != expectedEpisode.Value) isMatch = false;
            // Title matching can be fuzzy, but for Golden Standard we expect high fidelity.
            // We'll normalize simple things like casing.
            if (expectedTitle != null && !string.Equals(result.Title, expectedTitle, StringComparison.OrdinalIgnoreCase)) isMatch = false;

            if (isMatch)
            {
                passed++;
                _output.WriteLine($"[PASS] {relativePath}");
            }
            else
            {
                failures.Add(relativePath);
                _output.WriteLine($"[FAIL] {relativePath}");
                _output.WriteLine($"  Expected: {expectedMediaType} | {expectedTitle} ({expectedYear}) S{expectedSeason}E{expectedEpisode}");
                _output.WriteLine($"  Actual:   {result.MediaType} | {result.Title} ({result.Year}) S{result.Season}E{result.Episode}");
            }
        }

        double accuracy = (double)passed / total * 100;
        _output.WriteLine("--------------------------------------------------");
        _output.WriteLine($"Summary: {passed}/{total} passed ({accuracy:F2}%)");

        // We expect 100% accuracy for Tier A now that we have ported the logic
        Assert.Equal(100.0, accuracy);
    }

    private static string ResolveRepoFilePath(params string[] pathParts)
    {
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, Path.Combine(pathParts));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        throw new InvalidOperationException($"Could not resolve repo file path: {Path.Combine(pathParts)}");
    }
}
