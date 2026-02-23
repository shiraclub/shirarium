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
    private string _lastErrorOutput = string.Empty;
    private string? _activeModelPath;
    private string? _modelName;
    private string? _modelSource;
    private double _downloadProgress;
    private int _port = 11434;
    private Dictionary<string, string> _metadata = new();
    private CancellationTokenSource? _pollingCts;
    private DateTime? _startTime;
    private readonly List<string> _logBuffer = new();
    private readonly object _logLock = new();
    private const int MaxLogLines = 100;

    public InferenceManager(IApplicationPaths applicationPaths, ILogger<InferenceManager> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public (string Status, double Progress, string Error, int Port, Dictionary<string, string> Metadata, string ModelName, string ModelSource) GetStatus()
    {
        _metadata.TryGetValue("Model", out var liveModel);
        var name = liveModel ?? _modelName ?? "LLM";
        var source = _modelSource ?? "Local";
        return (_status, _downloadProgress, _error, _port, _metadata, name, source);
    }

    public string[] GetLogs()
    {
        lock (_logLock)
        {
            return _logBuffer.ToArray();
        }
    }

    private void AddLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_logLock)
        {
            _logBuffer.Add(line);
            if (_logBuffer.Count > MaxLogLines) _logBuffer.RemoveAt(0);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InferenceManager: Starting...");
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.EnableManagedLocalInference)
        {
            _status = "Disabled";
            return Task.CompletedTask;
        }

        if (_runnerProcess != null && !_runnerProcess.HasExited)
        {
            _status = "Ready";
            _startTime ??= DateTime.UtcNow;
            StartMetadataPolling();
            return Task.CompletedTask;
        }

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
                    return;
                }

                var modelPath = await EnsureModelExistsAsync(config, CancellationToken.None);
                if (string.IsNullOrEmpty(modelPath))
                {
                    _status = "Error";
                    _error = "Model file missing and download failed.";
                    return;
                }

                StartServer(binaryPath, modelPath, _port);
                
                // Status stays 'Initializing' until the first successful heartbeat or until a crash is detected
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InferenceManager stopping...");
        _pollingCts?.Cancel();
        _startTime = null;
        try
        {
            if (_runnerProcess != null && !_runnerProcess.HasExited) _runnerProcess.Kill();
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
        _ = Task.Run(async () =>
        {
            var url = $"http://127.0.0.1:{_port}/props";
            while (!token.IsCancellationRequested)
            {
                if (_runnerProcess == null || _runnerProcess.HasExited) 
                {
                    // Clean up metadata if process is gone
                    _metadata = new();
                    break;
                }

                try
                {
                    var newMeta = new Dictionary<string, string>();
                    if (_startTime.HasValue)
                    {
                        var diff = DateTime.UtcNow - _startTime.Value;
                        newMeta["Uptime"] = $"{(int)diff.TotalHours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2}.{diff.Milliseconds / 100}";
                    }
                    newMeta["Compute"] = DetectGpuLayers() > 0 ? "Vulkan/CUDA" : "CPU (AVX2)";
                    
                    try {
                        _runnerProcess.Refresh();
                        double memMb = _runnerProcess.WorkingSet64 / (1024.0 * 1024.0);
                        newMeta["RAM"] = memMb > 1024 ? $"{memMb / 1024.0:F2} GB" : $"{memMb:F0} MB";
                    } catch { newMeta["RAM"] = "0 MB"; }

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
                            if (rawName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) rawName = Path.GetFileNameWithoutExtension(rawName);
                            newMeta["Model"] = rawName;
                            if (root.TryGetProperty("n_ctx", out var ctx)) newMeta["Context"] = ctx.GetInt32().ToString();
                            
                            // Success! Transition to Ready if we were Initializing
                            if (_status == "Initializing") {
                                _status = "Ready";
                                _startTime = DateTime.UtcNow;
                            }
                        }
                    }
                    catch { 
                        // Engine might still be loading weights
                    }
                    _metadata = newMeta;
                }
                catch { }
                await Task.Delay(2000, token);
            }
        }, token);
    }

    private async Task<string> EnsureBinaryExistsAsync(CancellationToken cancellationToken)
    {
        var binFolder = Path.Combine(_applicationPaths.DataPath, "plugins", "Shirarium", "bin");
        Directory.CreateDirectory(binFolder);
        string binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "llama-server.exe" : "llama-server";
        var existing = Directory.GetFiles(binFolder, binaryName, SearchOption.AllDirectories).FirstOrDefault();
        if (existing != null) return existing;

        string? downloadUrl = GetBinaryUrl();
        if (downloadUrl == null) return string.Empty;

        _logger.LogInformation("Downloading llama-server binary...");
        var tempZip = Path.Combine(Path.GetTempPath(), $"shirarium-bin-{Guid.NewGuid():N}.zip");
        try
        {
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, cancellationToken);
            }
            ZipFile.ExtractToDirectory(tempZip, binFolder, true);
            var foundBinary = Directory.GetFiles(binFolder, binaryName, SearchOption.AllDirectories).FirstOrDefault();
            if (foundBinary != null && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try { Process.Start("chmod", $"+x \"{foundBinary}\"")?.WaitForExit(); } catch { }
            }
            return foundBinary ?? string.Empty;
        }
        catch { return string.Empty; }
        finally { if (File.Exists(tempZip)) File.Delete(tempZip); }
    }

    private string? GetBinaryUrl()
    {
        const string BaseUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b4640/";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return BaseUrl + "llama-b4640-bin-win-vulkan-x64.zip";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return BaseUrl + "llama-b4640-bin-ubuntu-x64.zip";
        return null;
    }

    private void StartServer(string binaryPath, string modelPath, int port)
    {
        _activeModelPath = modelPath;
        var startInfo = new ProcessStartInfo
        {
            FileName = binaryPath,
            Arguments = $"--model \"{modelPath}\" --port {port} --host 127.0.0.1 --n-gpu-layers {DetectGpuLayers()}",
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
            
            _runnerProcess.Exited += (s, e) => {
                var exitCode = _runnerProcess?.ExitCode ?? -1;
                _status = "Error";
                _error = $"Engine exited with code {exitCode}. Check logs.";
                _logger.LogError("LLM Process Exited with code {Code}", exitCode);
            };

            _runnerProcess.Start();

            // Background reader for Standard Output
            Task.Run(() => {
                try {
                    while (!_runnerProcess.HasExited && !_runnerProcess.StandardOutput.EndOfStream) {
                        var line = _runnerProcess.StandardOutput.ReadLine();
                        if (line != null) AddLog(line);
                    }
                } catch { }
            });

            // Background reader for Standard Error (Where llama-server logs errors)
            Task.Run(() => {
                try {
                    var fullErr = new System.Text.StringBuilder();
                    while (!_runnerProcess.HasExited && !_runnerProcess.StandardError.EndOfStream) {
                        var line = _runnerProcess.StandardError.ReadLine();
                        if (line != null) {
                            AddLog(line);
                            _lastErrorOutput = line;
                            fullErr.AppendLine(line);
                            _logger.LogInformation("LLM Engine: {Log}", line);
                        }
                    }
                    
                    // If we get here, the stream ended (process likely died)
                    var finalError = fullErr.ToString();
                    if (finalError.Contains("corrupted") || finalError.Contains("file bounds") || finalError.Contains("incomplete")) {
                        _status = "Error";
                        _error = "Model file corrupted. Purging...";
                        _logger.LogWarning("LLM Self-Healing: Deleting corrupted model: {Path}", _activeModelPath);
                        try { if (File.Exists(_activeModelPath)) File.Delete(_activeModelPath); } catch { }
                    }
                } catch { }
            });

            _logger.LogInformation("Local inference server started (PID: {Pid})", _runnerProcess.Id);
        }
        catch (Exception ex) { 
            _status = "Error";
            _error = ex.Message;
            _logger.LogError(ex, "Failed to start engine."); 
        }
    }

    private int DetectGpuLayers()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (File.Exists(Path.Combine(sys32, "nvcuda.dll")) || File.Exists(Path.Combine(sys32, "vulkan-1.dll"))) return 32;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (File.Exists("/usr/lib/x86_64-linux-gnu/libvulkan.so.1") || File.Exists("/usr/lib/x86_64-linux-gnu/libcuda.so")) return 32;
        }
        return 0;
    }

    private async Task<string> EnsureModelExistsAsync(PluginConfiguration config, CancellationToken cancellationToken)
    {
        var modelUrl = config.SelectedModelPreset == "custom" && !string.IsNullOrWhiteSpace(config.CustomModelUrl) ? config.CustomModelUrl : config.ModelUrl;
        var folder = Path.Combine(_applicationPaths.DataPath, "plugins", "Shirarium", "models");
        Directory.CreateDirectory(folder);

        string fileName = "model.gguf";
        if (Uri.TryCreate(modelUrl, UriKind.Absolute, out var uri))
        {
            _modelSource = uri.Host;
            fileName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) fileName = $"model-{config.SelectedModelPreset}.gguf";
        }
        else 
        {
            _modelSource = "Local Path";
        }
        
        _modelName = fileName;
        var modelPath = Path.Combine(folder, fileName);

        if (File.Exists(modelPath))
        {
            var info = new FileInfo(modelPath);
            if (info.Length > 500 * 1024 * 1024)
            {
                _downloadProgress = 100.0;
                return modelPath;
            }
            _logger.LogWarning("Model file too small, deleting: {Path}", modelPath);
            try { File.Delete(modelPath); } catch { }
        }

        _status = "DownloadingModel";
        _logger.LogInformation("Downloading model: {Url}", modelUrl);
        var tempPath = modelPath + ".download";
        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            using var response = await _httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true);
            var buffer = new byte[128 * 1024];
            long totalRead = 0;
            int read;
            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                totalRead += read;
                if (totalBytes.HasValue) _downloadProgress = (double)totalRead / totalBytes.Value * 100.0;
            }
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();
            
            // Success! Atomic rename
            if (File.Exists(modelPath)) File.Delete(modelPath);
            File.Move(tempPath, modelPath);
            return modelPath;
        }
        catch { if (File.Exists(tempPath)) File.Delete(tempPath); return string.Empty; }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _runnerProcess?.Kill();
        _runnerProcess?.Dispose();
        _httpClient.Dispose();
        _isDisposed = true;
    }
}
