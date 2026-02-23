using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Contracts;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Persistent cache for LLM parsing results to save CPU cycles on repeated scans.
/// </summary>
public sealed class ResultCacheStore
{
    private readonly string _cachePath;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private Dictionary<string, ParseFilenameResponse> _cache = new();
    private bool _isDirty;

    public ResultCacheStore(IApplicationPaths applicationPaths, ILogger logger)
    {
        _cachePath = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium", "result-cache.json");
        _logger = logger;
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;
            var json = File.ReadAllText(_cachePath);
            _cache = JsonSerializer.Deserialize<Dictionary<string, ParseFilenameResponse>>(json) ?? new();
            _logger.LogInformation("Loaded {Count} entries from result cache.", _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load result cache.");
        }
    }

    public void Save()
    {
        if (!_isDirty) return;
        try
        {
            lock (_lock)
            {
                var dir = Path.GetDirectoryName(_cachePath);
                if (dir != null) Directory.CreateDirectory(dir);
                
                var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_cachePath, json);
                _isDirty = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save result cache.");
        }
    }

    public ParseFilenameResponse? Get(string path, string engine, string model)
    {
        var key = GetKey(path, engine, model);
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var response))
            {
                return response;
            }
        }
        return null;
    }

    public void Set(string path, string engine, string model, ParseFilenameResponse response)
    {
        var key = GetKey(path, engine, model);
        lock (_lock)
        {
            _cache[key] = response;
            _isDirty = true;
        }
    }

    private static string GetKey(string path, string engine, string model)
    {
        // Simple key: path + engine + model name to ensure re-parse on model change
        return $"{path}|{engine}|{model}";
    }
}
