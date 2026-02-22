using RestSharp;
using System.Net;
using System.Linq;
using Xunit;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.SystemTests;

public class DeepAnalysisTests
{
    [Fact]
    public async Task EndToEnd_Organization_Flow_Consolidates_Scene_Folders_And_Syncs()
    {
        // 1. Setup Stack
        await using var stack = new ShirariumTestStack();
        await stack.StartAsync();
        
        var client = new JellyfinClient(stack.BaseUrl);
        await client.WaitForReadyAsync();
        
        // 2. Automated Initialization
        await client.CompleteWizardAsync();
        await client.AuthenticateAsync();
        
        // Verify Plugin is loaded
        var plugins = await client.GetPluginsAsync();
        Assert.Contains(plugins, p => p.Name == "Shirarium");
        
        // Setup Libraries: Downloads (Source), Movies (Target)
        // CRITICAL: Create directories on disk BEFORE adding them to Jellyfin
        Directory.CreateDirectory(Path.Combine(stack.MediaPath, "Downloads"));
        Directory.CreateDirectory(Path.Combine(stack.MediaPath, "Movies"));
        Directory.CreateDirectory(Path.Combine(stack.MediaPath, "TV"));

        await client.AddVirtualFolderAsync("Downloads", "movies", "/media/Downloads");
        await client.AddVirtualFolderAsync("Movies", "movies", "/media/Movies");
        
        // Assert Libraries exist with paths
        var vfs = await client.GetVirtualFoldersAsync();
        Assert.Contains(vfs, v => v.Name == "Downloads" && v.Locations.Contains("/media/Downloads"));

        // Enable Shirarium AI Parsing and set Organization Root
        var config = await client.GetConfigAsync();
        config.EnableAiParsing = true;
        config.DryRunMode = false;
        config.OrganizationRootPath = "/media/Movies";
        config.EnableFileOrganizationPlanning = true;
        config.EnablePostScanTask = false; // Disable to prevent race conditions
        await client.UpdateConfigAsync(config);

        // VERIFY CONFIG
        var updatedConfig = await client.GetConfigAsync();
        Assert.Equal("/media/Movies", updatedConfig.OrganizationRootPath);
        Assert.False(updatedConfig.EnablePostScanTask);

        // 3. Seed "Dirty" Data
        SeedComplexDataset(stack.MediaPath);
        
        // 4. Trigger Scan & Wait for completion
        await client.TriggerJellyfinScanAndWaitAsync();
        
        // 5. Assert: Shirarium Scan (with retries for path discovery)
        JellyfinClient.ScanSnapshotDto scanResult = new();
        for(int i=0; i<5; i++) {
            scanResult = await client.RunShirariumScanAsync();
            if (scanResult.CandidateCount > 0) break;
            await Task.Delay(2000);
        }
        
        Assert.True(scanResult.CandidateCount >= 2, $"Should find movies in Downloads. Last count: {scanResult.CandidateCount}");
        
        var suggestions = await client.GetSuggestionsAsync();
        var inception = suggestions.Suggestions.First(s => s.Path.Contains("Inception"));
        Assert.Equal("Inception", inception.SuggestedTitle);
        Assert.Equal(2010, inception.SuggestedYear);

        // 6. Assert: Plan Generation
        var plan = await client.GeneratePlanAsync();
        Assert.Equal(0, plan.ConflictCount);
        
        var inceptionEntry = plan.Entries.First(e => e.SourcePath.Contains("Inception"));
        Assert.Equal("move", inceptionEntry.Action);
        Assert.Contains("/media/Movies/Inception (2010) [1080p]/Inception (2010) [1080p]", inceptionEntry.TargetPath);

        // 7. Assert: Plan Application
        var status = await client.GetOpsStatusAsync();
        var selectedPaths = plan.Entries
            .Where(e => e.Action == "move")
            .Select(e => e.SourcePath)
            .ToArray();
            
        var applyResult = await client.ApplyPlanAsync(status.Plan.PlanFingerprint, selectedPaths);
        Assert.True(applyResult.AppliedCount > 0, "At least one item should be applied");
        Assert.Equal(0, applyResult.FailedCount);

        // 8. Physical Verification
        var targetFile = Path.Combine(stack.MediaPath, "Movies", "Inception (2010) [1080p]", "Inception (2010) [1080p].mkv");
        Assert.True(File.Exists(targetFile), $"File should have moved to: {targetFile}");
        
        // Scene folder should be gone
        var oldDir = Path.Combine(stack.MediaPath, "Downloads", "Inception.2010.1080p.BluRay.x264-SPARKS");
        Assert.False(Directory.Exists(oldDir), "Scene folder should have been cleaned up");

        // 9. Sync Verification
        await client.TriggerJellyfinScanAndWaitAsync();
        var movieItems = await client.GetLibraryItemsAsync("movies");
        Assert.Contains(movieItems, i => i.Name == "Inception");
    }

    private void SeedComplexDataset(string root)
    {
        var mkvHeader = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x00, 0x00, 0x00 };

        // Case 1: Scene Folder (Consolidation Target)
        var sceneDir = Path.Combine(root, "Downloads", "Inception.2010.1080p.BluRay.x264-SPARKS");
        Directory.CreateDirectory(sceneDir);
        File.WriteAllBytes(Path.Combine(sceneDir, "Inception.2010.1080p.mkv"), mkvHeader);
        File.WriteAllText(Path.Combine(sceneDir, "Inception.2010.1080p.nfo"), "<?xml version=\"1.0\" encoding=\"UTF-8\"?><movie><title>Inception</title><year>2010</year></movie>");
        // Sample.mkv removed to allow folder cleanup verification

        // Case 2: Flat File
        var flatFile = Path.Combine(root, "Downloads", "The.Matrix.1999.720p.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(flatFile)!);
        File.WriteAllBytes(flatFile, mkvHeader);
        
        // Ensure Target directories exist
        Directory.CreateDirectory(Path.Combine(root, "Movies"));
        Directory.CreateDirectory(Path.Combine(root, "TV"));
    }
}
