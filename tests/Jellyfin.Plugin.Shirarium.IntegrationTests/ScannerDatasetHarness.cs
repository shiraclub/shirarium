using Jellyfin.Plugin.Shirarium.Contracts;
using Jellyfin.Plugin.Shirarium.Services;

namespace Jellyfin.Plugin.Shirarium.IntegrationTests;

internal static class ScannerDatasetHarness
{
    internal static ScannerDatasetRuntime BuildRuntime(
        string rootPath,
        IEnumerable<SyntheticScannerItemSpec> specs)
    {
        var items = new List<object>();
        var responses = new Dictionary<string, ParseFilenameResponse?>(StringComparer.OrdinalIgnoreCase);
        var invocationCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in specs)
        {
            var fullPath = Path.Combine(
                rootPath,
                spec.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, spec.FileContents ?? "synthetic");

            var providerIds = spec.HasProviderIds
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tmdb"] = spec.ProviderIdValue ?? "12345"
                }
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            items.Add(new SyntheticLibraryItem
            {
                Id = spec.ItemId ?? Guid.NewGuid().ToString("N"),
                Name = spec.Name ?? Path.GetFileNameWithoutExtension(fullPath),
                Path = fullPath,
                ProviderIds = providerIds,
                Overview = spec.Overview,
                ProductionYear = spec.ProductionYear
            });

            responses[fullPath] = spec.ParseResponse;
            invocationCounts[fullPath] = 0;
        }

        return new ScannerDatasetRuntime
        {
            Provider = new StaticSourceCandidateProvider(responses.Keys),
            ParseFilenameAsync = (path, _) =>
            {
                if (invocationCounts.ContainsKey(path))
                {
                    invocationCounts[path]++;
                }

                responses.TryGetValue(path, out var response);
                return Task.FromResult(response);
            },
            ParseInvocationCounts = invocationCounts
        };
    }

    internal sealed class ScannerDatasetRuntime
    {
        public required ISourceCandidateProvider Provider { get; init; }

        public required Func<string, CancellationToken, Task<ParseFilenameResponse?>> ParseFilenameAsync { get; init; }

        public required IReadOnlyDictionary<string, int> ParseInvocationCounts { get; init; }
    }

    internal sealed class SyntheticScannerItemSpec
    {
        public required string RelativePath { get; init; }

        public string? ItemId { get; init; }

        public string? Name { get; init; }

        public bool HasProviderIds { get; init; }

        public string? ProviderIdValue { get; init; }

        public string? Overview { get; init; }

        public int? ProductionYear { get; init; }

        public ParseFilenameResponse? ParseResponse { get; init; }

        public string? FileContents { get; init; }
    }

    private sealed class StaticSourceCandidateProvider : ISourceCandidateProvider
    {
        private readonly string[] _items;

        public StaticSourceCandidateProvider(IEnumerable<string> items)
        {
            _items = items.ToArray();
        }

        public IEnumerable<string> GetCandidates(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _items;
        }
    }

    private sealed class SyntheticLibraryItem
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string Path { get; init; }

        public required Dictionary<string, string> ProviderIds { get; init; }

        public string? Overview { get; init; }

        public int? ProductionYear { get; init; }
    }
}

