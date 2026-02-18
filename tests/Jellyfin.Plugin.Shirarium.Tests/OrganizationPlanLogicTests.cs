using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class OrganizationPlanLogicTests
{
    [Fact]
    public void NormalizeSegment_RemovesReservedCharacters_AndCollapsesWhitespace()
    {
        var normalized = OrganizationPlanLogic.NormalizeSegment(" Noroi: The/Curse\\Cut   ");

        Assert.Equal("Noroi The Curse Cut", normalized);
    }

    [Fact]
    public void BuildEntry_ForMovie_UsesMovieFolderConvention()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi-source.mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Noroi",
                suggestedMediaType: "movie",
                suggestedYear: 2005);

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                Path.Combine(root, "organized"),
                normalizePathSegments: true);

            Assert.Equal("movie", entry.Strategy);
            Assert.Equal("move", entry.Action);
            Assert.Equal("Planned", entry.Reason);
            // Default template: {TitleWithYear} [{Resolution}]/{TitleWithYear} [{Resolution}]
            // Resolution is null, so cleanup removes " []"
            Assert.Equal(
                Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv"),
                entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_ForEpisode_UsesShowSeasonEpisodeConvention()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "file-01.mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Kowasugi",
                suggestedMediaType: "episode",
                suggestedSeason: 1,
                suggestedEpisode: 2);

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                Path.Combine(root, "organized"),
                normalizePathSegments: true);

            Assert.Equal("episode", entry.Strategy);
            Assert.Equal("move", entry.Action);
            // Default template: {Title}/Season {Season2}/{Title} S{Season2}E{Episode2} [{Resolution}]
            Assert.Equal(
                Path.Combine(root, "organized", "Kowasugi", "Season 01", "Kowasugi S01E02.mkv"),
                entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_WithMediaInfo_UsesTokensInPath()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "movie.mkv");
            var suggestion = new ScanSuggestion
            {
                Path = sourcePath,
                SuggestedTitle = "Matrix",
                SuggestedMediaType = "movie",
                SuggestedYear = 1999,
                Resolution = "1080p",
                VideoCodec = "HEVC",
                Confidence = 1.0
            };

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                Path.Combine(root, "organized"),
                normalizePathSegments: true);

            Assert.Equal(
                Path.Combine(root, "organized", "Matrix (1999) [1080p]", "Matrix (1999) [1080p].mkv"),
                entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_PlansAssociatedFiles_WhenTheyExist()
    {
        var root = CreateTempRoot();
        try
        {
            var incoming = Path.Combine(root, "incoming");
            var organized = Path.Combine(root, "organized");
            Directory.CreateDirectory(incoming);
            
            var videoPath = Path.Combine(incoming, "movie.mkv");
            var nfoPath = Path.Combine(incoming, "movie.nfo");
            var srtPath = Path.Combine(incoming, "movie.en.srt");
            
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(nfoPath, "nfo");
            File.WriteAllText(srtPath, "srt");

            var suggestion = CreateSuggestion(videoPath, "Movie", "movie", suggestedYear: 2024);
            suggestion = new ScanSuggestion 
            { 
                ItemId = suggestion.ItemId,
                Name = suggestion.Name,
                Path = suggestion.Path,
                SuggestedTitle = suggestion.SuggestedTitle,
                SuggestedMediaType = suggestion.SuggestedMediaType,
                SuggestedYear = suggestion.SuggestedYear,
                SuggestedSeason = suggestion.SuggestedSeason,
                SuggestedEpisode = suggestion.SuggestedEpisode,
                Confidence = suggestion.Confidence,
                Source = suggestion.Source,
                Resolution = "1080p" 
            };

            var entry = OrganizationPlanLogic.BuildEntry(suggestion, organized, true);

            Assert.Equal(2, entry.AssociatedFiles.Length);
            Assert.Contains(entry.AssociatedFiles, m => m.SourcePath == nfoPath && m.TargetPath.EndsWith("Movie (2024) [1080p].nfo"));
            Assert.Contains(entry.AssociatedFiles, m => m.SourcePath == srtPath && m.TargetPath.EndsWith("Movie (2024) [1080p].en.srt"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_ForMovie_UsesConfiguredTemplate()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi-source.mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Noroi",
                suggestedMediaType: "movie",
                suggestedYear: 2005);

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                Path.Combine(root, "organized"),
                normalizePathSegments: true,
                moviePathTemplate: "{Title}/Release {Year}/{TitleWithYear}",
                episodePathTemplate: OrganizationPlanLogic.DefaultEpisodePathTemplate);

            Assert.Equal("movie", entry.Strategy);
            Assert.Equal("move", entry.Action);
            Assert.Equal(
                Path.Combine(root, "organized", "Noroi", "Release 2005", "Noroi (2005).mkv"),
                entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_ForEpisode_UsesConfiguredTemplate()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "file-01.mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Kowasugi",
                suggestedMediaType: "episode",
                suggestedSeason: 1,
                suggestedEpisode: 2);

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                Path.Combine(root, "organized"),
                normalizePathSegments: true,
                moviePathTemplate: OrganizationPlanLogic.DefaultMoviePathTemplate,
                episodePathTemplate: "{Title}/S{Season2}/{Title} - {Episode2}");

            Assert.Equal("episode", entry.Strategy);
            Assert.Equal("move", entry.Action);
            Assert.Equal(
                Path.Combine(root, "organized", "Kowasugi", "S01", "Kowasugi - 02.mkv"),
                entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_WhenMovieTemplateHasUnknownToken_IsSkipped()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi-source.mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Noroi",
                suggestedMediaType: "movie",
                suggestedYear: 2005);

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                Path.Combine(root, "organized"),
                normalizePathSegments: true,
                moviePathTemplate: "{Title}/{UnknownToken}",
                episodePathTemplate: OrganizationPlanLogic.DefaultEpisodePathTemplate);

            Assert.Equal("movie", entry.Strategy);
            Assert.Equal("skip", entry.Action);
            Assert.Equal("InvalidMovieTemplate", entry.Reason);
            Assert.Null(entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_ForEpisodeWithoutSeasonOrEpisode_IsSkipped()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "unknown-ep.mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Kowasugi",
                suggestedMediaType: "episode");

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                Path.Combine(root, "organized"),
                normalizePathSegments: true);

            Assert.Equal("skip", entry.Action);
            Assert.Equal("MissingSeasonOrEpisode", entry.Reason);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_WhenTargetAlreadyExists_IsConflict()
    {
        var root = CreateTempRoot();
        try
        {
            var organizationRoot = Path.Combine(root, "organized");
            var expectedTarget = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedTarget)!);
            File.WriteAllText(expectedTarget, "existing");

            var sourcePath = Path.Combine(root, "incoming", "noroi-source.mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Noroi",
                suggestedMediaType: "movie",
                suggestedYear: 2005);

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                organizationRoot,
                normalizePathSegments: true);

            Assert.Equal("conflict", entry.Action);
            Assert.Equal("TargetAlreadyExists", entry.Reason);
            Assert.Equal(expectedTarget, entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildEntry_WhenAlreadyOrganized_IsNoop()
    {
        var root = CreateTempRoot();
        try
        {
            var organizationRoot = Path.Combine(root, "organized");
            var sourcePath = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv");
            var suggestion = CreateSuggestion(
                sourcePath,
                suggestedTitle: "Noroi",
                suggestedMediaType: "movie",
                suggestedYear: 2005);

            var entry = OrganizationPlanLogic.BuildEntry(
                suggestion,
                organizationRoot,
                normalizePathSegments: true);

            Assert.Equal("none", entry.Action);
            Assert.Equal("AlreadyOrganized", entry.Reason);
            Assert.Equal(sourcePath, entry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void MarkDuplicateTargetConflicts_ConvertsMoveEntriesToConflict()
    {
        var root = CreateTempRoot();
        try
        {
            var organizationRoot = Path.Combine(root, "organized");
            var suggestionA = CreateSuggestion(
                Path.Combine(root, "incoming", "a.mkv"),
                suggestedTitle: "Noroi",
                suggestedMediaType: "movie",
                suggestedYear: 2005);
            var suggestionB = CreateSuggestion(
                Path.Combine(root, "incoming", "b.mkv"),
                suggestedTitle: "Noroi",
                suggestedMediaType: "movie",
                suggestedYear: 2005);

            var entries = new List<OrganizationPlanEntry>
            {
                OrganizationPlanLogic.BuildEntry(suggestionA, organizationRoot, normalizePathSegments: true),
                OrganizationPlanLogic.BuildEntry(suggestionB, organizationRoot, normalizePathSegments: true)
            };

            OrganizationPlanLogic.MarkDuplicateTargetConflicts(entries);

            Assert.All(entries, entry =>
            {
                Assert.Equal("conflict", entry.Action);
                Assert.Equal("DuplicateTargetInPlan", entry.Reason);
            });
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void MarkDuplicateTargetConflicts_CaseOnlyTargetDifference_IsPlatformAware()
    {
        var entries = new List<OrganizationPlanEntry>
        {
            new()
            {
                SourcePath = "/media/incoming/A.mkv",
                TargetPath = "/media/organized/Noroi/Noroi.mkv",
                Action = "move",
                Reason = "Planned"
            },
            new()
            {
                SourcePath = "/media/incoming/B.mkv",
                TargetPath = "/media/organized/noroi/noroi.mkv",
                Action = "move",
                Reason = "Planned"
            }
        };

        OrganizationPlanLogic.MarkDuplicateTargetConflicts(entries);

        var conflictCount = entries.Count(entry => string.Equals(entry.Action, "conflict", StringComparison.OrdinalIgnoreCase));
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(2, conflictCount);
        }
        else
        {
            Assert.Equal(0, conflictCount);
        }
    }

    [Fact]
    public void BuildPlan_ComputesActionCounters()
    {
        var root = CreateTempRoot();
        try
        {
            var config = new PluginConfiguration
            {
                DryRunMode = true,
                OrganizationRootPath = Path.Combine(root, "organized"),
                NormalizePathSegments = true,
                MoviePathTemplate = OrganizationPlanLogic.DefaultMoviePathTemplate,
                EpisodePathTemplate = OrganizationPlanLogic.DefaultEpisodePathTemplate
            };

            var snapshot = new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateSuggestion(
                        Path.Combine(root, "incoming", "a.mkv"),
                        suggestedTitle: "Noroi",
                        suggestedMediaType: "movie",
                        suggestedYear: 2005),
                    CreateSuggestion(
                        Path.Combine(root, "incoming", "b.mkv"),
                        suggestedTitle: "Noroi",
                        suggestedMediaType: "movie",
                        suggestedYear: 2005),
                    CreateSuggestion(
                        Path.Combine(root, "incoming", "c.mkv"),
                        suggestedTitle: "Unknown",
                        suggestedMediaType: "episode")
                ]
            };

            var plan = OrganizationPlanner.BuildPlan(snapshot, config);

            Assert.Equal(3, plan.SourceSuggestionCount);
            Assert.Equal(0, plan.PlannedCount);
            Assert.Equal(0, plan.NoopCount);
            Assert.Equal(1, plan.SkippedCount);
            Assert.Equal(2, plan.ConflictCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildPlan_UsesConfiguredTemplates()
    {
        var root = CreateTempRoot();
        try
        {
            var config = new PluginConfiguration
            {
                DryRunMode = true,
                OrganizationRootPath = Path.Combine(root, "organized"),
                NormalizePathSegments = true,
                MoviePathTemplate = "{Title}/Films/{TitleWithYear}",
                EpisodePathTemplate = "{Title}/S{Season2}/{Title} - {Episode2}"
            };

            var snapshot = new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateSuggestion(
                        Path.Combine(root, "incoming", "a.mkv"),
                        suggestedTitle: "Noroi",
                        suggestedMediaType: "movie",
                        suggestedYear: 2005),
                    CreateSuggestion(
                        Path.Combine(root, "incoming", "b.mkv"),
                        suggestedTitle: "Kowasugi",
                        suggestedMediaType: "episode",
                        suggestedSeason: 1,
                        suggestedEpisode: 2)
                ]
            };

            var plan = OrganizationPlanner.BuildPlan(snapshot, config);
            var movieEntry = Assert.Single(plan.Entries, entry => entry.Strategy == "movie");
            var episodeEntry = Assert.Single(plan.Entries, entry => entry.Strategy == "episode");

            Assert.Equal(Path.Combine(root, "organized", "Noroi", "Films", "Noroi (2005).mkv"), movieEntry.TargetPath);
            Assert.Equal(Path.Combine(root, "organized", "Kowasugi", "S01", "Kowasugi - 02.mkv"), episodeEntry.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildPlan_WithSkipPolicy_SkipsExistingTargetConflicts()
    {
        var root = CreateTempRoot();
        try
        {
            var organizationRoot = Path.Combine(root, "organized");
            var existingTarget = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(existingTarget)!);
            File.WriteAllText(existingTarget, "existing");

            var config = new PluginConfiguration
            {
                DryRunMode = true,
                OrganizationRootPath = organizationRoot,
                NormalizePathSegments = true,
                TargetConflictPolicy = "skip"
            };

            var snapshot = new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateSuggestion(
                        Path.Combine(root, "incoming", "noroi-source.mkv"),
                        suggestedTitle: "Noroi",
                        suggestedMediaType: "movie",
                        suggestedYear: 2005)
                ]
            };

            var plan = OrganizationPlanner.BuildPlan(snapshot, config);
            var entry = Assert.Single(plan.Entries);

            Assert.Equal("skip", entry.Action);
            Assert.Equal("TargetAlreadyExists", entry.Reason);
            Assert.Equal(existingTarget, entry.TargetPath);
            Assert.Equal(0, plan.PlannedCount);
            Assert.Equal(1, plan.SkippedCount);
            Assert.Equal(0, plan.ConflictCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildPlan_WithSuffixPolicy_SuffixesExistingTargetConflicts()
    {
        var root = CreateTempRoot();
        try
        {
            var organizationRoot = Path.Combine(root, "organized");
            var existingTarget = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv");
            var existingSuffix2 = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005) (2).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(existingTarget)!);
            File.WriteAllText(existingTarget, "existing");
            File.WriteAllText(existingSuffix2, "existing-2");

            var config = new PluginConfiguration
            {
                DryRunMode = true,
                OrganizationRootPath = organizationRoot,
                NormalizePathSegments = true,
                TargetConflictPolicy = "suffix"
            };

            var snapshot = new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateSuggestion(
                        Path.Combine(root, "incoming", "noroi-source.mkv"),
                        suggestedTitle: "Noroi",
                        suggestedMediaType: "movie",
                        suggestedYear: 2005)
                ]
            };

            var plan = OrganizationPlanner.BuildPlan(snapshot, config);
            var entry = Assert.Single(plan.Entries);

            Assert.Equal("move", entry.Action);
            Assert.Equal("PlannedWithSuffix", entry.Reason);
            Assert.Equal(
                Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005) (3).mkv"),
                entry.TargetPath);
            Assert.Equal(1, plan.PlannedCount);
            Assert.Equal(0, plan.SkippedCount);
            Assert.Equal(0, plan.ConflictCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildPlan_WithSkipPolicy_SkipsDuplicateTargetsExceptFirst()
    {
        var root = CreateTempRoot();
        try
        {
            var organizationRoot = Path.Combine(root, "organized");
            var config = new PluginConfiguration
            {
                DryRunMode = true,
                OrganizationRootPath = organizationRoot,
                NormalizePathSegments = true,
                TargetConflictPolicy = "skip"
            };

            var sourceA = Path.Combine(root, "incoming", "a.mkv");
            var sourceB = Path.Combine(root, "incoming", "b.mkv");
            var snapshot = new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateSuggestion(sourceA, "Noroi", "movie", suggestedYear: 2005),
                    CreateSuggestion(sourceB, "Noroi", "movie", suggestedYear: 2005)
                ]
            };

            var plan = OrganizationPlanner.BuildPlan(snapshot, config);
            var moveEntry = Assert.Single(plan.Entries, entry => entry.Action == "move");
            var skippedEntry = Assert.Single(plan.Entries, entry => entry.Action == "skip");

            Assert.Equal(sourceA, moveEntry.SourcePath);
            Assert.Equal("DuplicateTargetInPlan", skippedEntry.Reason);
            Assert.Equal(1, plan.PlannedCount);
            Assert.Equal(1, plan.SkippedCount);
            Assert.Equal(0, plan.ConflictCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildPlan_WithSuffixPolicy_SuffixesDuplicateTargets()
    {
        var root = CreateTempRoot();
        try
        {
            var organizationRoot = Path.Combine(root, "organized");
            var config = new PluginConfiguration
            {
                DryRunMode = true,
                OrganizationRootPath = organizationRoot,
                NormalizePathSegments = true,
                TargetConflictPolicy = "suffix"
            };

            var sourceA = Path.Combine(root, "incoming", "a.mkv");
            var sourceB = Path.Combine(root, "incoming", "b.mkv");
            var snapshot = new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateSuggestion(sourceA, "Noroi", "movie", suggestedYear: 2005),
                    CreateSuggestion(sourceB, "Noroi", "movie", suggestedYear: 2005)
                ]
            };

            var plan = OrganizationPlanner.BuildPlan(snapshot, config);
            var moveEntries = plan.Entries.Where(entry => entry.Action == "move").ToArray();

            Assert.Equal(2, moveEntries.Length);
            Assert.Contains(moveEntries, entry => string.Equals(
                entry.TargetPath,
                Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv"),
                StringComparison.OrdinalIgnoreCase));
            Assert.Contains(moveEntries, entry => string.Equals(
                entry.TargetPath,
                Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005) (2).mkv"),
                StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, plan.PlannedCount);
            Assert.Equal(0, plan.SkippedCount);
            Assert.Equal(0, plan.ConflictCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static ScanSuggestion CreateSuggestion(
        string sourcePath,
        string suggestedTitle,
        string suggestedMediaType,
        int? suggestedYear = null,
        int? suggestedSeason = null,
        int? suggestedEpisode = null)
    {
        return new ScanSuggestion
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Name = Path.GetFileNameWithoutExtension(sourcePath),
            Path = sourcePath,
            SuggestedTitle = suggestedTitle,
            SuggestedMediaType = suggestedMediaType,
            SuggestedYear = suggestedYear,
            SuggestedSeason = suggestedSeason,
            SuggestedEpisode = suggestedEpisode,
            Confidence = 0.9,
            Source = "test"
        };
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "shirarium-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
        }
    }
}
