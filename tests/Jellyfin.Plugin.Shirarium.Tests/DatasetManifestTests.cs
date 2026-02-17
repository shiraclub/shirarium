using System.Text.Json;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class DatasetManifestTests
{
    [Fact]
    public void SyntheticCommunityManifest_IsWellFormed_AndCoversExpectedTypes()
    {
        var manifestPath = ResolveRepoFilePath("datasets", "jellyfin-dev", "synthetic-community-v1.json");
        var json = File.ReadAllText(manifestPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("synthetic-community-v1", root.GetProperty("name").GetString());

        var entries = root.GetProperty("entries");
        Assert.Equal(JsonValueKind.Array, entries.ValueKind);
        Assert.True(entries.GetArrayLength() >= 20, "Expected at least 20 seed entries.");

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mediaTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.EnumerateArray())
        {
            var relativePath = entry.GetProperty("relativePath").GetString();
            Assert.False(string.IsNullOrWhiteSpace(relativePath));
            Assert.StartsWith("incoming/", relativePath!, StringComparison.OrdinalIgnoreCase);
            Assert.False(Path.IsPathRooted(relativePath!));
            Assert.True(seenPaths.Add(relativePath!), $"Duplicate relativePath found: {relativePath}");

            var expected = entry.GetProperty("expected");
            var mediaType = expected.GetProperty("mediaType").GetString();
            Assert.False(string.IsNullOrWhiteSpace(mediaType));
            Assert.Contains(mediaType!, new[] { "movie", "episode", "ignored" });

            if (!mediaTypeCounts.TryAdd(mediaType!, 1))
            {
                mediaTypeCounts[mediaType!] += 1;
            }
        }

        Assert.True(mediaTypeCounts.TryGetValue("movie", out var movieCount) && movieCount > 0, "Expected at least one movie entry.");
        Assert.True(mediaTypeCounts.TryGetValue("episode", out var episodeCount) && episodeCount > 0, "Expected at least one episode entry.");
        Assert.True(mediaTypeCounts.TryGetValue("ignored", out var ignoredCount) && ignoredCount > 0, "Expected at least one ignored entry.");
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
