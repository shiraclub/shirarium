using RestSharp;
using System.Net;
using Xunit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Shirarium.SystemTests;

public class E2ETests
{
    [Fact(Skip = "Requires seeded Jellyfin configuration to bypass wizard automation issues.")]
    public async Task Full_Scan_Flow_Detects_Files_And_Generates_Plan()
    {
        // 1. Start Environment
        await using var stack = new ShirariumTestStack();
        await stack.StartAsync();
        
        var client = new JellyfinClient(stack.BaseUrl);
        await client.WaitForReadyAsync();
        
        // 2. Initialize Jellyfin
        await client.CompleteWizardAsync();
        
        // Wait for wizard completion to be acknowledged
        await client.WaitForWizardCompletionAsync();

        await client.AuthenticateAsync();
        
        // 3. Add Library
        // We add the library pointing to /media which is mounted from stack.MediaPath
        await client.AddMediaLibraryAsync();
        
        // 4. Seed Files (Simulate "Dirty" filesystem)
        SeedDirtyFiles(stack.MediaPath);
        
        // 5. Run Shirarium Scan
        // We wait a bit for Jellyfin to potentially lock files or start its own scan, 
        // but Shirarium filesystem provider should find them regardless.
        var scanResult = await client.RunShirariumScanAsync();
        
        Assert.True(scanResult.CandidateCount > 0, "Should find candidates on filesystem");
        Assert.Equal(0, scanResult.EngineFailureCount);

        // 6. Verify Suggestions
        var suggestions = await client.GetSuggestionsAsync();
        
        // Check for "Unrecognized" items (Jellyfin likely hasn't scanned them yet or matched them)
        var unrecognized = suggestions.Suggestions.FirstOrDefault(s => s.CandidateReasons.Contains("Unrecognized"));
        
        // Note: Jellyfin scan is fast for 1 file. It might have found it.
        // If it found it, reason is "Reorganization" + "MissingMetadata" (no IDs).
        // If it didn't, "Unrecognized".
        // Either way, we expect it to be in the list.
        Assert.NotNull(suggestions.Suggestions.FirstOrDefault(s => s.Path.Contains("My.Messy.Movie")));

        // 7. Verify Heuristic Parsing
        // The file "My.Messy.Movie.2024.1080p.mkv" should parse correctly.
        var item = suggestions.Suggestions.First(s => s.Path.Contains("My.Messy.Movie"));
        Assert.Equal("My Messy Movie", item.SuggestedTitle);
        Assert.Equal(2024, item.SuggestedYear);
        Assert.Equal("1080p", item.Resolution); // Extracted by Heuristics

        // 8. Generate Plan
        var plan = await client.GeneratePlanAsync();
        Assert.True(plan.PlannedCount > 0);
        
        var entry = plan.Entries.First(e => e.SourcePath.Contains("My.Messy.Movie"));
        Assert.Equal("move", entry.Action);
        Assert.Contains("My Messy Movie (2024)", entry.TargetPath);
    }

    private void SeedDirtyFiles(string root)
    {
        // Structure: /media/Downloads/My.Messy.Movie.2024.1080p-Group/My.Messy.Movie.2024.1080p.mkv
        var movieDir = Path.Combine(root, "Downloads", "My.Messy.Movie.2024.1080p-Group");
        Directory.CreateDirectory(movieDir);
        File.WriteAllText(Path.Combine(movieDir, "My.Messy.Movie.2024.1080p.mkv"), "dummy content");
        File.WriteAllText(Path.Combine(movieDir, "readme.txt"), "clutter");
        File.WriteAllText(Path.Combine(movieDir, "proof.jpg"), "clutter");
    }
}

public class JellyfinClient
{
    private readonly RestClient _client;
    private string _token = "";
    private string _userId = "";

    public JellyfinClient(string baseUrl)
    {
        _client = new RestClient(baseUrl);
    }

    public async Task WaitForReadyAsync()
    {
        for (int i = 0; i < 60; i++)
        {
            try
            {
                var req = new RestRequest("System/Info/Public", Method.Get);
                var resp = await _client.ExecuteAsync(req);
                if (resp.IsSuccessful && resp.ContentType != null && resp.ContentType.Contains("json"))
                {
                    return;
                }
            }
            catch { /* ignore connection errors */ }
            await Task.Delay(1000);
        }
        throw new Exception("Server did not become ready (JSON check timed out).");
    }

    public async Task CompleteWizardAsync()
    {
        // 1. Create User
        var userReq = new RestRequest("Startup/User", Method.Post);
        userReq.AddJsonBody(new { Name = "admin", Password = "admin" });
        var userResp = await _client.ExecuteAsync(userReq);
        // Ignore errors if user already exists (retry scenarios)

        // 2. Complete Config
        var confReq = new RestRequest("Startup/Configuration", Method.Post);
        confReq.AddJsonBody(new { UICulture = "en-US", MetadataCountryCode = "US", PreferredMetadataLanguage = "en" });
        await _client.ExecuteAsync(confReq);

        // 3. Finalize Wizard
        var compReq = new RestRequest("Startup/Complete", Method.Post);
        await _client.ExecuteAsync(compReq);
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

    private class PublicInfo { public bool StartupWizardCompleted { get; set; } }

    public async Task AuthenticateAsync()
    {
        var req = new RestRequest("Users/AuthenticateByName", Method.Post);
        req.AddHeader("X-Emby-Authorization", "MediaBrowser Client=\"Test\", Device=\"Test\", DeviceId=\"1\", Version=\"1\"");
        req.AddJsonBody(new { Username = "admin", Pw = "admin" });
        
        var resp = await _client.ExecuteAsync<AuthResponse>(req);
        if (!resp.IsSuccessful || resp.Data == null) throw new Exception("Auth failed: " + resp.Content);
        
        _token = resp.Data.AccessToken;
        _userId = resp.Data.SessionInfo.UserId;
        _client.AddDefaultHeader("X-Emby-Token", _token);
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

    public async Task<ScanSnapshotDto> RunShirariumScanAsync()
    {
        var req = new RestRequest("shirarium/scan", Method.Post);
        var resp = await _client.ExecuteAsync<ScanSnapshotDto>(req);
        if (!resp.IsSuccessful) throw new Exception("Scan failed: " + resp.Content);
        return resp.Data!;
    }

    public async Task<SuggestionsDto> GetSuggestionsAsync()
    {
        var req = new RestRequest("shirarium/suggestions", Method.Get);
        var resp = await _client.ExecuteAsync<SuggestionsDto>(req);
        return resp.Data!;
    }

    public async Task<PlanSnapshotDto> GeneratePlanAsync()
    {
        var req = new RestRequest("shirarium/plan-organize", Method.Post);
        var resp = await _client.ExecuteAsync<PlanSnapshotDto>(req);
        return resp.Data!;
    }

    private class AuthResponse { public string AccessToken { get; set; } = ""; public SessionInfo SessionInfo { get; set; } = new(); }
    private class SessionInfo { public string UserId { get; set; } = ""; }
    
    // DTOs matching plugin models loosely
    public class ScanSnapshotDto 
    { 
        public int CandidateCount { get; set; } 
        public int EngineFailureCount { get; set; }
    }
    public class SuggestionsDto { public List<SuggestionItem> Suggestions { get; set; } = new(); }
    public class SuggestionItem 
    { 
        public string Path { get; set; } = "";
        public string SuggestedTitle { get; set; } = "";
        public int? SuggestedYear { get; set; }
        public string? Resolution { get; set; }
        public string[] CandidateReasons { get; set; } = Array.Empty<string>();
    }
    public class PlanSnapshotDto 
    { 
        public int PlannedCount { get; set; } 
        public int ConflictCount { get; set; }
        public List<PlanEntry> Entries { get; set; } = new();
    }
    public class PlanEntry { public string SourcePath { get; set; } = ""; public string TargetPath { get; set; } = ""; public string Action { get; set; } = ""; }
}
