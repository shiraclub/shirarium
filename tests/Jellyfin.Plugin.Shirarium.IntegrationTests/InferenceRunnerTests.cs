using System.Runtime.InteropServices;
using Jellyfin.Plugin.Shirarium.Configuration;
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

            // Wait up to 2 minutes for it to reach a state that confirms binary extraction
            var start = DateTime.UtcNow;
            while ((status.Status == "Idle" || status.Status == "Initializing") && (DateTime.UtcNow - start).TotalMinutes < 2)
            {
                await Task.Delay(2000);
                status = manager.GetStatus();
            }

            Assert.NotEqual("Runner binary missing and download failed", status.Error);

            // If it reached DownloadingModel, it means EnsureBinaryExistsAsync worked
            Assert.True(status.Status == "Ready" || status.Status == "Initializing" || status.Status == "DownloadingModel",
                $"Expected Ready, Initializing, or DownloadingModel, but got {status.Status}. Error: {status.Error}");

            // Verify binary exists on disk
            var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "llama-server.exe" : "llama-server";
            var binDir = Path.Combine(tempRoot, "plugins", "Shirarium", "bin", InferenceBinaryProvider.Version);
            var files = Directory.GetFiles(binDir, binaryName, SearchOption.AllDirectories);
            Assert.True(files.Length > 0, $"Binary {binaryName} should exist in {binDir}. Found: {string.Join(", ", files)}");

            await manager.StopAsync(CancellationToken.None);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
