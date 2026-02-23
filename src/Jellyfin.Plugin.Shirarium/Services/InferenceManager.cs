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
/// Manages the lifecycle of the managed local LLM (llama.cpp).
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
        private int _port = 11434;
        private Dictionary<string, string> _metadata = new();
        private CancellationTokenSource? _pollingCts;
        private DateTime? _startTime;
    
        /// <summary>
        /// Initializes a new instance of the <see cref="InferenceManager"/> class.
        /// </summary>
        /// <param name="applicationPaths">Jellyfin application paths.</param>
        /// <param name="logger">Logger instance.</param>
        public InferenceManager(IApplicationPaths applicationPaths, ILogger<InferenceManager> logger)
        {
            _applicationPaths = applicationPaths;
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) }; 
        }
    
            /// <summary>
            /// Gets the current status of the LLM.
            /// </summary>
            public (string Status, double Progress, string Error, int Port, Dictionary<string, string> Metadata) GetStatus()
            {
                return (_status, _downloadProgress, _error, _port, _metadata);
            }    
            /// <inheritdoc />
            public Task StartAsync(CancellationToken cancellationToken)
            {
                _logger.LogInformation("InferenceManager: Starting...");
        
                var config = Plugin.Instance?.Configuration;
                if (config == null || !config.EnableManagedLocalInference)
                {
                    _status = "Disabled";
                    return Task.CompletedTask;
                }
        
                // If already running, just ensure the clock and polling are alive
                if (_runnerProcess != null && !_runnerProcess.HasExited)
                {
                    _status = "Ready";
                    _startTime ??= DateTime.UtcNow;
                    StartMetadataPolling();
                    return Task.CompletedTask;
                }
        
                // We run the heavy setup in a separate task to not block server startup
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _status = "Initializing";
                        _port = config.InferencePort;
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
        
                        StartServer(binaryPath, modelPath, _port);
                        
                        _startTime = DateTime.UtcNow;
                        _status = "Ready";
                        _downloadProgress = 100.0;
                        
                        StartMetadataPolling();
                    }
                    catch (Exception ex)
                    {
                        _status = "Error";
                        _error = ex.Message;
                        _logger.LogError(ex, "Error initializing local LLM.");
                    }
                }, cancellationToken);
        
                return Task.CompletedTask;
            }
    
        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("InferenceManager stopping...");
            _pollingCts?.Cancel();
            _startTime = null;
            try
            {
                if (_runnerProcess != null && !_runnerProcess.HasExited)
                {
                    _runnerProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill inference runner process.");
            }
            return Task.CompletedTask;
        }
    
                    private void StartMetadataPolling()
                    {
                        _pollingCts?.Cancel();
                        _pollingCts = new CancellationTokenSource();
                        var token = _pollingCts.Token;
                
                        _logger.LogInformation("LLM Heartbeat: Starting polling loop on port {Port}...", _port);
                
                        _ = Task.Run(async () =>
                        {
                            var url = $"http://127.0.0.1:{_port}/props";
                            
                            while (!token.IsCancellationRequested)
                            {
                                if (_runnerProcess == null || _runnerProcess.HasExited) 
                                {
                                    _logger.LogWarning("LLM Heartbeat: LLM process is not active. Stopping polling.");
                                    break;
                                }
                
                                try
                                {
                                    var newMeta = new Dictionary<string, string>();
                                    
                                    // 1. Calculate Uptime
                                    if (_startTime.HasValue)
                                    {
                                        var diff = DateTime.UtcNow - _startTime.Value;
                                        newMeta["Uptime"] = $"{(int)diff.TotalHours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2}.{diff.Milliseconds / 100}";
                                    }
                                    else
                                    {
                                        newMeta["Uptime"] = "00:00:00.0";
                                    }
                
                                                                        // 2. Hardware Info
                                                                        newMeta["Compute"] = DetectGpuLayers() > 0 ? "Vulkan/CUDA" : "CPU (AVX2)";
                                                                        
                                                                        _runnerProcess.Refresh();
                                                                        double memMb = _runnerProcess.WorkingSet64 / (1024.0 * 1024.0);
                                                                        newMeta["RAM"] = memMb > 1024 ? $"{memMb / 1024.0:F2} GB" : $"{memMb:F0} MB";
                                    
                                                                        // 3. Try fetch internal state
                                                                        try
                                                                        {
                                                                            using var response = await _httpClient.GetAsync(url, token);
                                                                            if (response.IsSuccessStatusCode)
                                                                            {
                                                                                var json = await response.Content.ReadAsStringAsync(token);
                                                                                using var doc = System.Text.Json.JsonDocument.Parse(json);
                                                                                var root = doc.RootElement;
                                    
                                                                                string rawName = "Loaded";
                                                                                if (root.TryGetProperty("model_path", out var mp)) rawName = Path.GetFileName(mp.GetString()) ?? "Loaded";
                                                                                else if (root.TryGetProperty("model_name", out var mn)) rawName = mn.GetString() ?? "Loaded";
                                                                                
                                                                                // Clean up model name (remove extension)
                                                                                if (rawName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                                                                                {
                                                                                    rawName = Path.GetFileNameWithoutExtension(rawName);
                                                                                }
                                                                                newMeta["Model"] = rawName;
                                                                                
                                                                                // Context might be top-level or in default_generation_settings
                                                                                if (root.TryGetProperty("n_ctx", out var ctx)) 
                                                                                {
                                                                                    newMeta["Context"] = ctx.GetInt32().ToString();
                                                                                }
                                                                                else if (root.TryGetProperty("default_generation_settings", out var dgs) && dgs.TryGetProperty("n_ctx", out var ctxNested))
                                                                                {
                                                                                    newMeta["Context"] = ctxNested.GetInt32().ToString();
                                                                                }
                                    
                                                                                _logger.LogInformation("LLM Heartbeat: Pulse OK. Uptime: {Uptime} | RAM: {Ram}", newMeta["Uptime"], newMeta["RAM"]);
                                                                            }                                        else
                                        {
                                            _logger.LogWarning("LLM Heartbeat: Engine responded with {Code} at {Url}", response.StatusCode, url);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Model weights are likely still loading into RAM/VRAM
                                        _logger.LogInformation("LLM Heartbeat: Waiting for engine weights... ({Msg})", ex.Message);
                                    }
                
                                    // 4. Atomic swap
                                    _metadata = newMeta;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "LLM Heartbeat: Fatal loop error.");
                                }
                
                                await Task.Delay(2000, token);
                            }
                        }, token);
                    }    private async Task<string> EnsureBinaryExistsAsync(CancellationToken cancellationToken)
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
            
            // Safer extraction: only delete if possible, otherwise overwrite.
            try 
            {
                if (Directory.Exists(binFolder))
                {
                    // Attempt to delete. If it fails (file in use), we'll try to just extract over it.
                    Directory.Delete(binFolder, true);
                    Directory.CreateDirectory(binFolder);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning("Could not clean bin folder (likely in use), attempting to extract over existing files. Error: {Message}", ex.Message);
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

    private void StartServer(string binaryPath, string modelPath, int port)
    {
        if (_runnerProcess != null && !_runnerProcess.HasExited)
        {
            return;
        }

        _logger.LogInformation("Starting local inference server on port {Port}...", port);

        int gpuLayers = DetectGpuLayers();
        if (gpuLayers > 0)
        {
            _logger.LogInformation("Detected GPU availability. Setting n-gpu-layers to {Layers}.", gpuLayers);
        }
        else
        {
            _logger.LogInformation("No compatible GPU detected. Falling back to CPU-only inference.");
        }

                var startInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    Arguments = $"--model \"{modelPath}\" --port {port} --host 127.0.0.1 --n-gpu-layers {gpuLayers}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(binaryPath)
                };

        try
        {
            _runnerProcess = new Process { StartInfo = startInfo };
            _runnerProcess.EnableRaisingEvents = true;
            _runnerProcess.Exited += (sender, args) =>
            {
                if (sender is Process p)
                {
                    var exitCode = p.ExitCode;
                    var error = p.StandardError.ReadToEnd();
                                        var output = p.StandardOutput.ReadToEnd();
                                        
                                        _logger.LogError("LLM Process Crashed! ExitCode: {Code}", exitCode);
                                        if (!string.IsNullOrWhiteSpace(error)) _logger.LogError("LLM STDERR: {Err}", error);
                                        if (!string.IsNullOrWhiteSpace(output)) _logger.LogError("LLM STDOUT: {Out}", output);
                    
                                        _status = "Error";
                                        if (error.Contains("libgomp") || error.Contains("shared object"))
                                        {
                                            _error = "Missing dependency: libgomp1. (apt-get install libgomp1)";
                                        }
                                        else if (error.Contains("CUDA") || error.Contains("driver"))
                                        {
                                            _error = "GPU Driver Error. Try disabling GPU in settings.";
                                        }
                                        else
                                        {
                                            _error = $"Process exited with code {exitCode}. Check logs.";
                                        }
                                    }
                                };            
            _runnerProcess.Start();
            _logger.LogInformation("Local inference server started (PID: {Pid})", _runnerProcess.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start local inference server.");
        }
    }

    private int DetectGpuLayers()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Check for common GPU drivers/runtimes
                var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (File.Exists(Path.Combine(system32, "nvcuda.dll")) || 
                    File.Exists(Path.Combine(system32, "vulkan-1.dll")))
                {
                    return 32; // Enable GPU layers
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Basic check for Vulkan or NVIDIA on Linux
                if (File.Exists("/usr/lib/x86_64-linux-gnu/libvulkan.so.1") || 
                    File.Exists("/usr/lib/x86_64-linux-gnu/libcuda.so"))
                {
                    return 32;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GPU detection failed, falling back to CPU.");
        }

        return 0; // Fallback to CPU
    }

    private async Task<string> EnsureModelExistsAsync(PluginConfiguration config, CancellationToken cancellationToken)
    {
        var modelUrl = config.ModelUrl;
        var modelPath = config.LocalModelPath;

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            var folder = Path.Combine(_applicationPaths.DataPath, "plugins", "Shirarium", "models");
            Directory.CreateDirectory(folder);

            // Use a unique filename based on the URL to allow multiple models to coexist
            string fileName;
            if (Uri.TryCreate(modelUrl, UriKind.Absolute, out var uri))
            {
                fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = $"model-{config.SelectedModelPreset}.gguf";
                }
            }
            else
            {
                fileName = "shirarium-custom-model.gguf";
            }

            modelPath = Path.Combine(folder, fileName);
        }

        if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 1024 * 1024)
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

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed) return;
    
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _runnerProcess?.Kill();
            _runnerProcess?.Dispose();
            _httpClient.Dispose();
    
            _isDisposed = true;
        }}
