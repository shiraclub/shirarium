using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Shirarium.Contracts;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Service for communicating with the local LLM inference engine (Ollama or llama-server).
/// </summary>
public sealed class OllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    
    private const string SystemPrompt = @"You are Shirarium-Core, a high-precision metadata extraction engine.
TASK: Extract media metadata from the provided path.
OUTPUT: Strict JSON only. No markdown, no conversational filler.

LOGIC RULES:
1. TITLE vs YEAR: If a title looks like a year (e.g., '1917', '2012'), use context to disambiguate. 
2. ABSOLUTE NUMBERING: For anime, 3-4 digit numbers (e.g., '1050') are likely absolute episodes, not years.
3. SCRIPT FIDELITY: Preserve original scripts (CJK, Cyrillic) exactly. DO NOT transliterate.

SCHEMA:
{
  ""title"": ""string"",
  ""media_type"": ""movie"" | ""episode"" | ""unknown"",
  ""year"": integer | null,
  ""season"": integer | null,
  ""episode"": integer | null,
  ""confidence"": float
}";

    public OllamaService(ILogger<OllamaService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Attempts to parse the filename using the local LLM.
    /// </summary>
    public async Task<ParseFilenameResponse?> ParseAsync(string path, CancellationToken cancellationToken)
    {
        // TODO: Get port/url from config if customizable. For now, default to managed port.
        var baseUrl = "http://localhost:11434";
        var url = $"{baseUrl}/v1/chat/completions";

        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Path: {path}" }
            },
            temperature = 0.0,
            max_tokens = 128,
            stream = false
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(cancellationToken: cancellationToken);
            var content = jsonResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            // 1. Strip <think> blocks
            content = Regex.Replace(content, @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();

            // 2. Extract JSON if wrapped in markdown
            var jsonMatch = Regex.Match(content, @"\{.*\}", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                content = jsonMatch.Value;
            }

            var result = JsonSerializer.Deserialize<ParseFilenameResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            if (result != null)
            {
                // Enforce source
                return new ParseFilenameResponse
                {
                    Title = result.Title,
                    MediaType = result.MediaType,
                    Year = result.Year,
                    Season = result.Season,
                    Episode = result.Episode,
                    Confidence = result.Confidence,
                    Source = "ollama",
                    RawTokens = new[] { content.Length > 50 ? content.Substring(0, 50) : content }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama inference failed for path: {Path}", path);
        }

        return null;
    }

    // Helper classes for OpenAI API response
    private class OpenAiChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; set; }
    }

    private class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
