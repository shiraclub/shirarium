using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

public static class SuggestionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "dryrun-suggestions.json");
    }

    public static ScanResultSnapshot Read(IApplicationPaths applicationPaths)
    {
        var filePath = GetFilePath(applicationPaths);
        if (!File.Exists(filePath))
        {
            return new ScanResultSnapshot();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ScanResultSnapshot>(json, JsonOptions) ?? new ScanResultSnapshot();
        }
        catch
        {
            return new ScanResultSnapshot();
        }
    }

    public static async Task WriteAsync(
        IApplicationPaths applicationPaths,
        ScanResultSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(applicationPaths);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
