using System.Runtime.InteropServices;
using Jellyfin.Plugin.Shirarium.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.IntegrationTests;

public sealed class InferenceRunnerTests
{
    [Fact]
    public async Task EnsureBinary_DownloadsAndExtracts_ForCurrentPlatform()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "shirarium-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        
        try
        {
            var appPaths = new TestApplicationPaths 
            { 
                DataPath = tempRoot 
            };
            
            var config = new PluginConfiguration
            {
                EnableManagedLocalInference = true,
                SelectedModelPreset = "gemma-3-4b",
                ModelUrl = "https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF/resolve/main/google_gemma-3-4b-it-Q6_K.gguf",
                InferencePort = 11434
            };
            
            var manager = new InferenceManager(appPaths, NullLogger<InferenceManager>.Instance, config);
            
            // This will trigger the download and extraction
            // We use a timeout to prevent hanging the CI if there's a network issue
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            
            // We need to use Reflection or make the method internal to test it directly, 
            // but we can also just call StartAsync which calls it.
            // Let's call StartAsync to verify the full lifecycle.
            await manager.StartAsync(cts.Token);
            
            var status = manager.GetStatus();
            
            // It might still be "Downloading" or "Initializing" depending on speed,
            // but we want to see it eventually reach "Ready" or at least not fail with "Binary missing".
            
            // Wait up to 2 minutes for it to become Ready (heartbeat check)
            var start = DateTime.UtcNow;
            while (status.Status != "Ready" && (DateTime.UtcNow - start).TotalMinutes < 2)
            {
                await Task.Delay(2000);
                status = manager.GetStatus();
            }

            Assert.NotEqual("Runner binary missing and download failed", status.Error);
            Assert.True(status.Status == "Ready" || status.Status == "Initializing", 
                $"Expected Ready or Initializing, but got {status.Status}. Error: {status.Error}");

            await manager.StopAsync(CancellationToken.None);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
