using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using RestSharp;
using System.Net;

namespace Jellyfin.Plugin.Shirarium.SystemTests;

public class ShirariumTestStack : IAsyncDisposable
{
    private readonly IContainer _jellyfinContainer;
    private readonly string _mediaPath;
    private readonly string _pluginTempPath;

    public string BaseUrl => $"http://{_jellyfinContainer.Hostname}:{_jellyfinContainer.GetMappedPublicPort(8096)}";
    public string MediaPath => _mediaPath;

    public ShirariumTestStack()
    {
        var repoRoot = FindRepoRoot();
        _mediaPath = Path.Combine(Path.GetTempPath(), "shirarium-sys-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mediaPath);

        var buildOutputPath = Path.Combine(repoRoot, "src", "Jellyfin.Plugin.Shirarium", "bin", "Release", "net9.0");
        if (!File.Exists(Path.Combine(buildOutputPath, "Jellyfin.Plugin.Shirarium.dll")))
        {
            throw new FileNotFoundException($"Plugin DLL not found at {buildOutputPath}. Build the project in Release mode first.");
        }

        // Copy plugin to temp to ensure clean state and R/W access for the container
        _pluginTempPath = Path.Combine(Path.GetTempPath(), "shirarium-sys-plugin", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pluginTempPath);
        foreach (var file in Directory.GetFiles(buildOutputPath))
        {
            File.Copy(file, Path.Combine(_pluginTempPath, Path.GetFileName(file)));
        }

        _jellyfinContainer = new ContainerBuilder()
            .WithImage("jellyfin/jellyfin:10.11.6")
            .WithPortBinding(8096, true)
            .WithBindMount(_mediaPath, "/media")
            .WithBindMount(_pluginTempPath, "/config/plugins/Shirarium")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(8096)
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8096).ForPath("/System/Info/Public")))
            .Build();
    }

    public async Task StartAsync()
    {
        await _jellyfinContainer.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _jellyfinContainer.DisposeAsync();
        CleanupDir(_mediaPath);
        CleanupDir(_pluginTempPath);
    }

    private static void CleanupDir(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch { /* ignore */ }
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Shirarium.sln"))) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new Exception("Repo root not found (searched for Shirarium.sln).");
    }
}
