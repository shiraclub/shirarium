using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using Jellyfin.Plugin.Shirarium.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Manages the lifecycle of the managed local inference engine (llama.cpp).
/// </summary>
public sealed class InferenceManager : IDisposable
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private Process? _runnerProcess;
    private bool _isDisposed;

    public InferenceManager(IApplicationPaths applicationPaths, ILogger logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromHours(1) }; // Models are large
    }

    /// <summary>
    /// Ensures the local inference engine is ready, downloading model and binaries if necessary.
    /// </summary>
    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.EnableManagedLocalInference)
        {
            return;
        }

        var binaryPath = await EnsureBinaryExistsAsync(cancellationToken);
        if (string.IsNullOrEmpty(binaryPath))
        {
            _logger.LogWarning("Managed local inference enabled but runner binary is missing and could not be downloaded.");
            return;
        }

        var modelPath = await EnsureModelExistsAsync(config, cancellationToken);
        if (string.IsNullOrEmpty(modelPath))
        {
            _logger.LogWarning("Managed local inference enabled but model file is missing.");
            return;
        }

        StartServer(binaryPath, modelPath);
    }

    private async Task<string> EnsureBinaryExistsAsync(CancellationToken cancellationToken)
    {
        var binFolder = Path.Combine(_applicationPaths.DataPath, "plugins", "Shirarium", "bin");
        Directory.CreateDirectory(binFolder);

        string binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "llama-server.exe" : "llama-server";
        var binaryPath = Path.Combine(binFolder, binaryName);

        if (File.Exists(binaryPath))
        {
            return binaryPath;
        }

        string? downloadUrl = GetBinaryUrl();
        if (downloadUrl == null)
        {
            _logger.LogError("Automatic binary download not supported for this platform: {OS} {Arch}", 
                RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture);
            return string.Empty;
        }

        _logger.LogInformation("Downloading llama-server binary for {OS}...", RuntimeInformation.OSDescription);
        
        var tempZip = Path.Combine(Path.GetTempPath(), $"shirarium-bin-{Guid.NewGuid():N}.zip");
        try
        {
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken);
                }
            }

            _logger.LogInformation("Extracting binary...");
            ZipFile.ExtractToDirectory(tempZip, binFolder, true);
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Set executable permission on Unix
                Process.Start("chmod", $"+x \"{binaryPath}\"")?.WaitForExit();
            }

            _logger.LogInformation("Binary setup complete.");
            return binaryPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup runner binary from {Url}", downloadUrl);
            return string.Empty;
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    private string? GetBinaryUrl()
    {
        // Example: Using a tagged release of llama.cpp or a custom mirror
        const string BaseUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b4640/";
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
            return BaseUrl + "llama-b4640-bin-win-vulkan-x64.zip"; // We'd need to unzip this in a real impl
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
            return BaseUrl + "llama-b4640-bin-ubuntu-x64.zip";

        return null;
    }

    private void StartServer(string binaryPath, string modelPath)
    {
        if (_runnerProcess != null && !_runnerProcess.HasExited)
        {
            return;
        }

        _logger.LogInformation("Starting local inference server...");

        var startInfo = new ProcessStartInfo
        {
            FileName = binaryPath,
            Arguments = $"--model \"{modelPath}\" --port 11434 --n-gpu-layers 0", // Default to CPU for maximum compatibility
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(binaryPath)
        };

        try
        {
            _runnerProcess = Process.Start(startInfo);
            _logger.LogInformation("Local inference server started (PID: {Pid})", _runnerProcess?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start local inference server.");
        }
    }

    private async Task<string> EnsureModelExistsAsync(PluginConfiguration config, CancellationToken cancellationToken)
    {
        var modelPath = config.LocalModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            var folder = Path.Combine(_applicationPaths.DataPath, "plugins", "Shirarium", "models");
            Directory.CreateDirectory(folder);
            modelPath = Path.Combine(folder, "qwen3-4b-instruct.gguf");
        }

        if (File.Exists(modelPath))
        {
            return modelPath;
        }

        if (!config.AutoDownloadModel)
        {
            return string.Empty;
        }

        _logger.LogInformation("Downloading recommended LLM model to {Path}...", modelPath);
        
        try
        {
            using var response = await _httpClient.GetAsync(config.ModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await response.Content.CopyToAsync(fileStream, cancellationToken);
            
            _logger.LogInformation("Model download complete.");
            return modelPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download LLM model from {Url}", config.ModelUrl);
            if (File.Exists(modelPath)) File.Delete(modelPath);
            return string.Empty;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _runnerProcess?.Kill();
        _runnerProcess?.Dispose();
        _httpClient.Dispose();
        
        _isDisposed = true;
    }
}
