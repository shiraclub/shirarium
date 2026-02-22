using Jellyfin.Plugin.Shirarium.Contracts;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Client for the Shirarium organization engine.
/// Now acts as an orchestrator between the native HeuristicParser and the local OllamaService.
/// </summary>
public sealed class EngineClient
{
    private readonly HeuristicParser _heuristicParser;
    private readonly OllamaService _ollamaService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineClient"/> class.
    /// </summary>
    /// <param name="heuristicParser">The native heuristic parser.</param>
    /// <param name="ollamaService">The local LLM service.</param>
    public EngineClient(HeuristicParser heuristicParser, OllamaService ollamaService)
    {
        _heuristicParser = heuristicParser;
        _ollamaService = ollamaService;
    }

    /// <summary>
    /// Parses a filename to extract metadata using heuristics, falling back to AI if enabled and needed.
    /// </summary>
    /// <param name="path">The full file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parse result.</returns>
    public async Task<ParseFilenameResponse?> ParseFilenameAsync(string path, CancellationToken cancellationToken)
    {
        // 1. Heuristic Parse (Zero latency)
        var result = _heuristicParser.Parse(path);

        // 2. Check Configuration for AI
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.EnableAiParsing)
        {
            return result;
        }

        bool useAi = config.EnableManagedLocalInference || !string.IsNullOrWhiteSpace(config.ExternalOllamaUrl);

        // 3. AI Fallback logic
        if (useAi && (result.Confidence < 0.90 || result.MediaType == "unknown"))
        {
            var aiResult = await _ollamaService.ParseAsync(path, cancellationToken);
            if (aiResult != null)
            {
                // If AI is confident, return it.
                // We could implement more complex merging here later.
                return aiResult;
            }
        }

        return result;
    }

    /// <summary>
    /// Performs a benchmark of the parsing engines using standard test cases.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Benchmark results.</returns>
    public async Task<object> BenchmarkAsync(CancellationToken cancellationToken)
    {
        string[] testCases = 
        [
            "/media/Downloads/Inception.2010.1080p.BluRay.x264-SPARKS/Inception.2010.1080p.mkv",
            "/media/Downloads/[SubsPlease] Shingeki no Kyojin - S4E12 (1080p) [ABC12345].mkv",
            "/media/Downloads/The.Matrix.1999.720p.BRRip.x264-YIFY.mp4",
            "/media/Downloads/Everything.Everywhere.All.At.Once.2022.2160p.UHD.BluRay.x265.10bit.HDR.DV.TrueHD.7.1.Atmos-SWTYBLZ.mkv",
            "/media/Downloads/S01E01.Pilot.Internal.1080p.WEB.H264-GROUP.mkv"
        ];

        var results = new List<object>();
        var sw = new System.Diagnostics.Stopwatch();

        foreach (var path in testCases)
        {
            sw.Restart();
            var heuristic = _heuristicParser.Parse(path);
            sw.Stop();
            var heuristicMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            var ai = await _ollamaService.ParseAsync(path, cancellationToken);
            sw.Stop();
            var aiMs = sw.Elapsed.TotalMilliseconds;

            results.Add(new
            {
                Path = path,
                Heuristic = new { heuristic.Title, heuristic.Confidence, LatencyMs = heuristicMs },
                Ai = ai != null ? (object)new { ai.Title, ai.Confidence, LatencyMs = aiMs } : null
            });
        }

        return new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Results = results
        };
    }
}
