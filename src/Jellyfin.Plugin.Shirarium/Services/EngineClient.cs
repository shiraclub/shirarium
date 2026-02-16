using System.Net.Http.Json;
using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Contracts;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// HTTP client wrapper for communicating with the Shirarium engine service.
/// </summary>
public sealed class EngineClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client instance.</param>
    public EngineClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Parses a media filename/path using the configured engine endpoint.
    /// </summary>
    /// <param name="path">Media path or filename to parse.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parse result or <c>null</c> if parsing is disabled or fails.</returns>
    public async Task<ParseFilenameResponse?> ParseFilenameAsync(string path, CancellationToken cancellationToken = default)
    {
        var plugin = Plugin.Instance;
        if (plugin is null || !plugin.Configuration.EnableAiParsing)
        {
            return null;
        }

        var baseUrl = plugin.Configuration.EngineBaseUrl.TrimEnd('/');
        var request = new ParseFilenameRequest { Path = path };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/v1/parse-filename",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ParseFilenameResponse>(cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
