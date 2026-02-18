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
}
