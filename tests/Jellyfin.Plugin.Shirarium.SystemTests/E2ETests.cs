using RestSharp;
using System.Net;
using Xunit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Shirarium.SystemTests;

public class E2ETests
{
    [Fact]
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
        // Create directories first
        Directory.CreateDirectory(Path.Combine(stack.MediaPath, "Downloads"));
        await client.AddMediaLibraryAsync();
        
        // 4. Seed Files (Simulate "Dirty" filesystem)
        SeedDirtyFiles(stack.MediaPath);
        
        // 5. Run Shirarium Scan
        var scanResult = await client.RunShirariumScanAsync();
        
        Assert.True(scanResult.CandidateCount > 0, "Should find candidates on filesystem");
        Assert.Equal(0, scanResult.EngineFailureCount);

        // 6. Verify Suggestions
        var suggestions = await client.GetSuggestionsAsync();
        
        Assert.NotNull(suggestions.Suggestions.FirstOrDefault(s => s.Path.Contains("My.Messy.Movie")));

        // 7. Verify Heuristic Parsing
        var item = suggestions.Suggestions.First(s => s.Path.Contains("My.Messy.Movie"));
        Assert.Equal("My Messy Movie", item.SuggestedTitle);
        Assert.Equal(2024, item.SuggestedYear);
        Assert.Equal("1080p", item.Resolution); 

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
