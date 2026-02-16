using System.Net.Http.Json;
using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Contracts;

namespace Jellyfin.Plugin.Shirarium.Services;

public sealed class EngineClient
{
    private readonly HttpClient _httpClient;

    public EngineClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

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
