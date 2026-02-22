using RestSharp;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Shirarium.SystemTests;

public class JellyfinClient
{
    private readonly RestClient _client;
    private string _token = "";
    private const string AuthHeaderValue = "MediaBrowser Client=\"SmokeTest\", Device=\"CI\", DeviceId=\"smoke-1\", Version=\"1.0.0\"";

    public JellyfinClient(string baseUrl)
    {
        _client = new RestClient(baseUrl);
        _client.AddDefaultHeader("X-Emby-Authorization", AuthHeaderValue);
    }

    public async Task WaitForReadyAsync()
    {
        for (int i = 0; i < 120; i++)
        {
            try {
                var resp = await _client.ExecuteAsync(new RestRequest("System/Info/Public", Method.Get));
                if (resp.IsSuccessful && resp.ContentType != null && resp.ContentType.Contains("json"))
                {
                    var info = JsonSerializer.Deserialize<PublicInfo>(resp.Content!);
                    // On fresh 10.11.6, StartupWizardCompleted should be false but present
                    if (info != null && !string.IsNullOrEmpty(info.Version)) return;
                }
            } catch { }
            await Task.Delay(1000);
        }
        throw new Exception("Timed out waiting for Jellyfin to become ready.");
    }

    public async Task CompleteWizardAsync()
    {
        // Give server a bit more time to settle after readiness
        await Task.Delay(2000);

        // Check if user already exists
        var existingUserResp = await _client.ExecuteAsync(new RestRequest("Startup/User", Method.Get));
        bool userExists = existingUserResp.IsSuccessful && !string.IsNullOrEmpty(existingUserResp.Content) && existingUserResp.Content.Contains("root");

        // 1. Set Config
        for (int i = 0; i < 5; i++)
        {
            var req = new RestRequest("Startup/Configuration", Method.Post)
                .AddJsonBody(new { UICulture = "en-US", MetadataCountryCode = "US", PreferredMetadataLanguage = "en" });
            var resp = await _client.ExecuteAsync(req);
            if (resp.IsSuccessful) break;
            await Task.Delay(2000);
        }
        
        // 2. Create admin User with fixed password
        if (!userExists)
        {
            bool userCreated = false;
            for (int i = 0; i < 10; i++)
            {
                var req = new RestRequest("Startup/User", Method.Post)
                    .AddJsonBody(new { Name = "root", Password = "root", PasswordConfirm = "root" });
                var resp = await _client.ExecuteAsync(req);
                
                if (resp.IsSuccessful || resp.StatusCode == HttpStatusCode.BadRequest) 
                {
                    userCreated = true;
                    break;
                }
                await Task.Delay(2000);
            }
            if (!userCreated) throw new Exception("Failed to create startup user after retries.");
        }
        else
        {
            // Even if user exists, it might have no password. 
            // We'll proceed to complete and handle password in AuthenticateAsync
        }

        // 3. Complete
        await _client.ExecuteAsync(new RestRequest("Startup/Complete", Method.Post));
        
        await Task.Delay(5000); // Wait longer for DB commit
    }

    public async Task AuthenticateAsync()
    {
        Exception? lastEx = null;
        string[] passwordsToTry = { "root", "" };

        for (int i = 0; i < 30; i++)
        {
            foreach (var password in passwordsToTry)
            {
                try
                {
                    var req = new RestRequest("Users/AuthenticateByName", Method.Post);
                    req.AddHeader("X-Emby-Authorization", AuthHeaderValue);
                    req.AddJsonBody(new { Username = "root", Pw = password });
                    var resp = await _client.ExecuteAsync<AuthResponse>(req);

                    if (resp.IsSuccessful && resp.Data != null)
                    {
                        _token = resp.Data.AccessToken;
                        _client.AddDefaultHeader("X-Emby-Token", _token);
                        return;
                    }
                    
                    lastEx = new Exception($"Auth failed for user 'root' with password '{(password == "" ? "[empty]" : password)}': {resp.StatusCode} {resp.Content}");
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }
            await Task.Delay(1000);
        }
        throw lastEx ?? new Exception("Authentication failed after retries.");
    }

    public async Task AddVirtualFolderAsync(string name, string type, string path)
    {
        var req = new RestRequest("Library/VirtualFolders", Method.Post);
        req.AddQueryParameter("name", name);
        req.AddQueryParameter("collectionType", type);
        req.AddQueryParameter("paths", path); 
        req.AddQueryParameter("refreshLibrary", "true");
        var resp = await _client.ExecuteAsync(req);
        if (!resp.IsSuccessful) throw new Exception($"Failed to add virtual folder {name}: {resp.StatusCode} {resp.Content}");

        // Wait for library to be acknowledged
        for (int i = 0; i < 15; i++)
        {
            var vfs = await GetVirtualFoldersAsync();
            if (vfs.Any(v => v.Name == name)) return;
            await Task.Delay(1000);
        }
    }

    public async Task<List<VirtualFolderDto>> GetVirtualFoldersAsync()
    {
        var resp = await _client.ExecuteAsync<List<VirtualFolderDto>>(new RestRequest("Library/VirtualFolders", Method.Get));
        return resp.Data ?? new List<VirtualFolderDto>();
    }

    private string? _pluginId;

    public async Task<string> GetShirariumPluginIdAsync()
    {
        if (_pluginId != null) return _pluginId;
        var plugins = await GetPluginsAsync();
        var plugin = plugins.FirstOrDefault(p => p.Name == "Shirarium");
        if (plugin == null) throw new Exception("Shirarium plugin not found in Jellyfin.");
        _pluginId = plugin.Id;
        return _pluginId;
    }

    public async Task<PluginConfigurationDto> GetConfigAsync()
    {
        var id = await GetShirariumPluginIdAsync();
        var resp = await _client.ExecuteAsync<PluginConfigurationDto>(new RestRequest($"Plugins/{id}/Configuration", Method.Get));
        if (!resp.IsSuccessful) throw new Exception($"Failed to get config: {resp.StatusCode} {resp.Content}");
        return resp.Data!;
    }

    public async Task UpdateConfigAsync(PluginConfigurationDto config)
    {
        var id = await GetShirariumPluginIdAsync();
        var url = _client.BuildUri(new RestRequest($"Plugins/{id}/Configuration"));
        
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-Emby-Token", _token);
        
        // Jellyfin core expects PascalCase for plugin configurations usually
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { PropertyNamingPolicy = null });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var resp = await httpClient.PostAsync(url, content);
        if (!resp.IsSuccessStatusCode) 
        {
            var errorBody = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Failed to update config: {resp.StatusCode} {errorBody}");
        }
        
        // Give plugin time to react to config change
        await Task.Delay(2000);
    }

    public async Task TriggerJellyfinScanAndWaitAsync()
    {
        var tasks = await _client.ExecuteAsync<List<ScheduledTaskDto>>(new RestRequest("ScheduledTasks", Method.Get));
        var task = tasks.Data?.FirstOrDefault(t => t.Key == "RefreshLibrary");
        if (task == null) return;

        await _client.ExecuteAsync(new RestRequest($"ScheduledTasks/Running/{task.Id}", Method.Post));

        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(1000);
            var updated = await _client.ExecuteAsync<List<ScheduledTaskDto>>(new RestRequest("ScheduledTasks", Method.Get));
            var currentTask = updated.Data?.FirstOrDefault(t => t.Id == task.Id);
            if (currentTask != null && currentTask.State == "Idle") return;
        }
    }

    public async Task WaitForWizardCompletionAsync()
    {
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var req = new RestRequest("System/Info/Public", Method.Get);
                var resp = await _client.ExecuteAsync<PublicInfo>(req);
                if (resp.IsSuccessful && resp.Data != null && resp.Data.StartupWizardCompleted)
                {
                    return;
                }
            }
            catch { /* ignore */ }
            await Task.Delay(1000);
        }
        throw new Exception("Wizard completion timed out.");
    }

    public async Task AddMediaLibraryAsync()
    {
        var req = new RestRequest("Library/VirtualFolders", Method.Post);
        req.AddQueryParameter("Name", "Media");
        req.AddQueryParameter("CollectionType", "movies");
        req.AddQueryParameter("RefreshLibrary", "true");
        
        var body = new
        {
            LibraryOptions = new
            {
                EnableRealtimeMonitor = false,
                PathInfos = new[] { new { Path = "/media" } }
            }
        };
        req.AddJsonBody(body);
        
        await _client.ExecuteAsync(req);
    }

    public async Task<ScanSnapshotDto> RunShirariumScanAsync() => (await _client.ExecuteAsync<ScanSnapshotDto>(new RestRequest("shirarium/scan", Method.Post))).Data!;
    public async Task<SuggestionsDto> GetSuggestionsAsync() => (await _client.ExecuteAsync<SuggestionsDto>(new RestRequest("shirarium/suggestions", Method.Get))).Data!;
    public async Task<PlanSnapshotDto> GeneratePlanAsync() => (await _client.ExecuteAsync<PlanSnapshotDto>(new RestRequest("shirarium/plan-organize", Method.Post))).Data!;
    public async Task<OpsStatusDto> GetOpsStatusAsync() => (await _client.ExecuteAsync<OpsStatusDto>(new RestRequest("shirarium/ops-status", Method.Get))).Data!;
    
    public async Task<InferenceStatusDto> GetInferenceStatusAsync() 
    {
        var resp = await _client.ExecuteAsync<InferenceStatusDto>(new RestRequest("shirarium/inference-status", Method.Get));
        return resp.Data!;
    }

    public async Task<TestTemplateResponseDto> TestTemplateAsync(string path)
    {
        var req = new RestRequest("shirarium/test-template", Method.Post);
        req.AddJsonBody(new { path });
        var resp = await _client.ExecuteAsync<TestTemplateResponseDto>(req);
        if (!resp.IsSuccessful) throw new Exception($"TestTemplate failed: {resp.StatusCode} {resp.Content}");
        return resp.Data!;
    }

    public async Task<ApplyResultDto> ApplyPlanAsync(string fingerprint, string[]? sourcePaths = null)
    {
        var req = new RestRequest("shirarium/apply-plan", Method.Post);
        req.AddJsonBody(new 
        { 
            expectedPlanFingerprint = fingerprint,
            sourcePaths = sourcePaths ?? Array.Empty<string>()
        });
        var resp = await _client.ExecuteAsync<ApplyResultDto>(req);
        if (!resp.IsSuccessful) throw new Exception($"Apply failed: {resp.StatusCode} {resp.Content}");
        return resp.Data!;
    }
    public async Task<List<JellyfinItemDto>> GetLibraryItemsAsync(string type)
    {
        var req = new RestRequest("Items", Method.Get);
        req.AddQueryParameter("Recursive", "true");
        req.AddQueryParameter("IncludeItemTypes", type == "movies" ? "Movie" : "Series");
        var resp = await _client.ExecuteAsync<JellyfinQueryResult>(req);
        return resp.Data?.Items ?? new List<JellyfinItemDto>();
    }
    public async Task<List<PluginDto>> GetPluginsAsync() => (await _client.ExecuteAsync<List<PluginDto>>(new RestRequest("Plugins", Method.Get))).Data ?? new();

    private class PublicInfo { public string Version { get; set; } = ""; public bool StartupWizardCompleted { get; set; } }
    private class AuthResponse 
    { 
        public string AccessToken { get; set; } = ""; 
        public SessionInfo? SessionInfo { get; set; }
        public UserInfo? User { get; set; }
    }
    private class SessionInfo { public string UserId { get; set; } = ""; }
    private class UserInfo { public string Id { get; set; } = ""; }
    private class ScheduledTaskDto { public string Id { get; set; } = ""; public string Key { get; set; } = ""; public string State { get; set; } = ""; }
    
    public class InferenceStatusDto
    {
        public string Status { get; set; } = "";
        public double Progress { get; set; }
        public string? Error { get; set; }
    }

    public class TestTemplateResponseDto
    {
        public string SourcePath { get; set; } = "";
        public string TargetPath { get; set; } = "";
        public string Action { get; set; } = "";
        public MetadataDto Metadata { get; set; } = new();
    }

    public class MetadataDto
    {
        public string Title { get; set; } = "";
        public string MediaType { get; set; } = "";
        public int? Year { get; set; }
        public string Source { get; set; } = "";
        public double Confidence { get; set; }
    }

    public class ScanSnapshotDto { public int CandidateCount { get; set; } public int ParseFailureCount { get; set; } }
    public class SuggestionsDto { public List<SuggestionItem> Suggestions { get; set; } = new(); }
    public class SuggestionItem 
    { 
        public string Path { get; set; } = ""; 
        public string SuggestedTitle { get; set; } = ""; 
        public int? SuggestedYear { get; set; } 
        public string? Resolution { get; set; }
        public string[] CandidateReasons { get; set; } = Array.Empty<string>();
    }
    public class PlanSnapshotDto { public int PlannedCount { get; set; } public int ConflictCount { get; set; } public List<PlanEntry> Entries { get; set; } = new(); }
    public class PlanEntry { public string SourcePath { get; set; } = ""; public string TargetPath { get; set; } = ""; public string Action { get; set; } = ""; }
    public class OpsStatusDto { public PlanStatusDto Plan { get; set; } = new(); }
    public class PlanStatusDto { public string PlanFingerprint { get; set; } = ""; }
    public class ApplyResultDto 
    { 
        public bool Success { get; set; } 
        public int AppliedCount { get; set; }
        public int FailedCount { get; set; }
        public int RequestedCount { get; set; }
    }
    public class JellyfinQueryResult { public List<JellyfinItemDto> Items { get; set; } = new(); }
    public class JellyfinItemDto { public string Name { get; set; } = ""; }
    public class PluginDto { public string Name { get; set; } = ""; public string Id { get; set; } = ""; }
    public class VirtualFolderDto { public string Name { get; set; } = ""; public List<string> Locations { get; set; } = new(); }
    public class PluginConfigurationDto
    {
        public string ExternalOllamaUrl { get; set; } = string.Empty;
        public bool EnableAiParsing { get; set; } = true;
        public bool DryRunMode { get; set; } = true;
        public bool EnablePostScanTask { get; set; } = true;
        public int MaxItemsPerRun { get; set; } = 500;
        public double MinConfidence { get; set; } = 0.55;
        public string[] ScanFileExtensions { get; set; } = Array.Empty<string>();
        public bool EnableFileOrganizationPlanning { get; set; } = true;
        public bool EnableManagedLocalInference { get; set; } = true;
        public int InferencePort { get; set; } = 11434;
        public bool AutoDownloadModel { get; set; } = true;
        public string LocalModelPath { get; set; } = string.Empty;
        public string ModelUrl { get; set; } = string.Empty;
        public string OrganizationRootPath { get; set; } = "/media";
        public bool NormalizePathSegments { get; set; } = true;
        public string MoviePathTemplate { get; set; } = string.Empty;
        public string EpisodePathTemplate { get; set; } = string.Empty;
        public string TargetConflictPolicy { get; set; } = "fail";
    }
}
