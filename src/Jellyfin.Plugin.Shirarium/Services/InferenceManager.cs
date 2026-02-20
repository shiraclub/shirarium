using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using Jellyfin.Plugin.Shirarium.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Manages the lifecycle of the managed local inference engine (llama.cpp).
/// </summary>
public sealed class InferenceManager : IHostedService, IDisposable
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private Process? _runnerProcess;
    private bool _isDisposed;
    private string _status = "Idle";
    private string _error = string.Empty;
    private double _downloadProgress;

    public InferenceManager(IApplicationPaths applicationPaths, ILogger<InferenceManager> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromHours(1) }; // Models are large
    }

    /// <summary>
    /// Gets the current status of the inference engine.
    /// </summary>
    public (string Status, double Progress, string Error) GetStatus()
    {
        if (_runnerProcess != null && !_runnerProcess.HasExited)
        {
            return ("Ready", 100.0, string.Empty);
        }

        return (_status, _downloadProgress, _error);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InferenceManager starting...");
        
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.EnableManagedLocalInference)
        {
            _status = "Disabled";
            return;
        }

        // We run the heavy setup in a separate task to not block server startup
        _ = Task.Run(async () => 
        {
            try 
            {
                _status = "Initializing";
                var binaryPath = await EnsureBinaryExistsAsync(CancellationToken.None);
                if (string.IsNullOrEmpty(binaryPath))
                {
                    _status = "Error";
                    _error = "Runner binary missing and download failed.";
                    _logger.LogWarning(_error);
                    return;
                }

                var modelPath = await EnsureModelExistsAsync(config, CancellationToken.None);
                if (string.IsNullOrEmpty(modelPath))
                {
                    _status = "Error";
                    _error = "Model file missing and download failed.";
                    _logger.LogWarning(_error);
                    return;
                }

                StartServer(binaryPath, modelPath);
                _status = "Ready";
            }
            catch (Exception ex)
            {
                _status = "Error";
                _error = ex.Message;
                _logger.LogError(ex, "Error initializing local inference engine.");
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InferenceManager stopping...");
        _runnerProcess?.Kill();
        return Task.CompletedTask;
    }

    private async Task<string> EnsureBinaryExistsAsync(CancellationToken cancellationToken)
    {
        var binFolder = Path.Combine(_applicationPaths.DataPath, "plugins", "Shirarium", "bin");
        Directory.CreateDirectory(binFolder);

        string binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "llama-server.exe" : "llama-server";
        
        // Check if binary exists anywhere in bin folder
        var existing = Directory.GetFiles(binFolder, binaryName, SearchOption.AllDirectories).FirstOrDefault();
        if (existing != null)
        {
            return existing;
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
            // Clean bin folder before extraction to avoid conflicts/mess
            if (Directory.Exists(binFolder))
            {
                Directory.Delete(binFolder, true);
                Directory.CreateDirectory(binFolder);
            }
            
            ZipFile.ExtractToDirectory(tempZip, binFolder, true);
            
            var foundBinary = Directory.GetFiles(binFolder, binaryName, SearchOption.AllDirectories).FirstOrDefault();
            if (foundBinary == null)
            {
                _logger.LogError("Binary {Name} not found in extracted zip.", binaryName);
                return string.Empty;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Set executable permission on Unix
                try
                {
                    using var chmod = Process.Start("chmod", $"+x \"{foundBinary}\"");
                    chmod?.WaitForExit();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set executable permissions on {Path}", foundBinary);
                }
            }

            _logger.LogInformation("Binary setup complete: {Path}", foundBinary);
            return foundBinary;
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
            return BaseUrl + "llama-b4640-bin-win-vulkan-x64.zip";
        
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
            modelPath = Path.Combine(folder, "shirarium-model.gguf");
        }

        if (File.Exists(modelPath))
        {
            _downloadProgress = 100.0;
            return modelPath;
        }

        if (!config.AutoDownloadModel)
        {
            return string.Empty;
        }

        _status = "DownloadingModel";
        _logger.LogInformation("Downloading recommended LLM model to {Path}...", modelPath);
        
        try
        {
            using var response = await _httpClient.GetAsync(config.ModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalRead = 0L;
            int read;
            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                totalRead += read;
                if (totalBytes.HasValue)
                {
                    _downloadProgress = (double)totalRead / totalBytes.Value * 100.0;
                }
            }
            
            _downloadProgress = 100.0;
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
