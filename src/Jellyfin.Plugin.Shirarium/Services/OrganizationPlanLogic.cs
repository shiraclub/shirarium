using System.Text;
using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanLogic
{
    internal const string DefaultMoviePathTemplate = "{TitleWithYear} [{Resolution}]/{TitleWithYear} [{Resolution}]";
    internal const string DefaultEpisodePathTemplate = "{Title}/Season {Season2}/{Title} S{Season2}E{Episode2} [{Resolution}]";
    internal const string DefaultTargetConflictPolicy = "fail";

    internal static string NormalizeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "Unknown";
        }

        segment = segment.Normalize(NormalizationForm.FormKC);
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(segment.Length);
        foreach (var ch in segment)
        {
            if (invalid.Contains(ch))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(ch switch
            {
                '/' or '\\' or ':' => ' ',
                _ => ch
            });
        }

        var collapsed = string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var trimmed = collapsed.Trim().Trim('.');
        return string.IsNullOrWhiteSpace(trimmed) ? "Unknown" : trimmed;
    }

    internal static OrganizationPlanEntry BuildEntry(
        ScanSuggestion suggestion,
        string rootPath,
        bool normalizePathSegments)
    {
        return BuildEntry(
            suggestion,
            rootPath,
            normalizePathSegments,
            DefaultMoviePathTemplate,
            DefaultEpisodePathTemplate);
    }

    internal static OrganizationPlanEntry BuildEntry(
        ScanSuggestion suggestion,
        string rootPath,
        bool normalizePathSegments,
        string moviePathTemplate,
        string episodePathTemplate)
    {
        var entry = new OrganizationPlanEntry
        {
            ItemId = suggestion.ItemId,
            SourcePath = suggestion.Path,
            Confidence = suggestion.Confidence,
            SuggestedTitle = suggestion.SuggestedTitle,
            SuggestedMediaType = suggestion.SuggestedMediaType
        };

        if (string.IsNullOrWhiteSpace(suggestion.Path))
        {
            entry.Action = "skip";
            entry.Reason = "MissingSourcePath";
            return entry;
        }

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            entry.Action = "skip";
            entry.Reason = "MissingOrganizationRootPath";
            return entry;
        }

        var ext = Path.GetExtension(suggestion.Path);
        if (string.IsNullOrWhiteSpace(ext))
        {
            entry.Action = "skip";
            entry.Reason = "MissingFileExtension";
            return entry;
        }

        var title = normalizePathSegments
            ? NormalizeSegment(suggestion.SuggestedTitle)
            : suggestion.SuggestedTitle?.Trim();
        title = string.IsNullOrWhiteSpace(title) ? "Unknown Title" : title;

        var effectiveMovieTemplate = string.IsNullOrWhiteSpace(moviePathTemplate)
            ? DefaultMoviePathTemplate
            : moviePathTemplate;
        var effectiveEpisodeTemplate = string.IsNullOrWhiteSpace(episodePathTemplate)
            ? DefaultEpisodePathTemplate
            : episodePathTemplate;

        string? targetPath = null;
        if (string.Equals(suggestion.SuggestedMediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            entry.Strategy = "movie";
            var movieTokens = BuildMovieTemplateTokens(title, suggestion.SuggestedYear, suggestion);
            if (!TryRenderRelativePath(
                    effectiveMovieTemplate,
                    movieTokens,
                    normalizePathSegments,
                    out var relativeMoviePath))
            {
                entry.Action = "skip";
                entry.Reason = "InvalidMovieTemplate";
                return entry;
            }

            targetPath = BuildTargetPath(rootPath, relativeMoviePath!, ext);
        }
        else if (string.Equals(suggestion.SuggestedMediaType, "episode", StringComparison.OrdinalIgnoreCase))
        {
            entry.Strategy = "episode";

            if (!suggestion.SuggestedSeason.HasValue || !suggestion.SuggestedEpisode.HasValue)
            {
                entry.Action = "skip";
                entry.Reason = "MissingSeasonOrEpisode";
                return entry;
            }

            var episodeTokens = BuildEpisodeTemplateTokens(
                title,
                suggestion.SuggestedSeason.Value,
                suggestion.SuggestedEpisode.Value,
                suggestion);
            if (!TryRenderRelativePath(
                    effectiveEpisodeTemplate,
                    episodeTokens,
                    normalizePathSegments,
                    out var relativeEpisodePath))
            {
                entry.Action = "skip";
                entry.Reason = "InvalidEpisodeTemplate";
                return entry;
            }

            targetPath = BuildTargetPath(rootPath, relativeEpisodePath!, ext);
        }
        else
        {
            entry.Action = "skip";
            entry.Strategy = "unknown";
            entry.Reason = "UnsupportedMediaType";
            return entry;
        }

        entry.TargetPath = targetPath;
        if (PathEquals(suggestion.Path, targetPath))
        {
            entry.Action = "none";
            entry.Reason = "AlreadyOrganized";
            return entry;
        }

        if (File.Exists(targetPath) && !PathEquals(suggestion.Path, targetPath))
        {
            entry.Action = "conflict";
            entry.Reason = "TargetAlreadyExists";
            return entry;
        }

        entry.Action = "move";
        entry.Reason = "Planned";

        entry.AssociatedFiles = DiscoverAssociatedFiles(suggestion.Path, targetPath, suggestion.SuggestedMediaType);

        return entry;
    }

    private static AssociatedFileMove[] DiscoverAssociatedFiles(
        string sourceVideoPath, 
        string targetVideoPath, 
        string? mediaType)
    {
        var sourceDir = Path.GetDirectoryName(sourceVideoPath);
        var targetDir = Path.GetDirectoryName(targetVideoPath);
        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(sourceDir))
        {
            return [];
        }

        var videoFileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceVideoPath);
        var targetFileNameWithoutExt = Path.GetFileNameWithoutExtension(targetVideoPath);
        var associatedMoves = new List<AssociatedFileMove>();

        // 1. Strict name matches (e.g. Movie.en.srt, Movie.nfo)
        var filesInDir = Directory.GetFiles(sourceDir);
        foreach (var file in filesInDir)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith(videoFileNameWithoutExt, StringComparison.OrdinalIgnoreCase) 
                && !PathEquals(file, sourceVideoPath))
            {
                var suffix = fileName.Substring(videoFileNameWithoutExt.Length);
                associatedMoves.Add(new AssociatedFileMove
                {
                    SourcePath = file,
                    TargetPath = Path.Combine(targetDir, targetFileNameWithoutExt + suffix)
                });
            }
        }

        // 2. Common assets if this is likely a private folder (Movie or Season folder)
        if (IsLikelyPrivateFolder(sourceDir))
        {
            AddCommonAssets(sourceDir, targetDir, associatedMoves);

            // 3. Known subdirectories (e.g. Subs, extras)
            var commonDirs = new[] { "Subs", "extras", "featurettes", "Specials", "behind the scenes", "Featurettes" };
            foreach (var commonDir in commonDirs)
            {
                var sourcePath = Path.Combine(sourceDir, commonDir);
                if (Directory.Exists(sourcePath))
                {
                    associatedMoves.Add(new AssociatedFileMove
                    {
                        SourcePath = sourcePath,
                        TargetPath = Path.Combine(targetDir, commonDir)
                    });
                }
            }

            // 4. Series-level aggregation (for TV shows)
            if (mediaType?.Equals("episode", StringComparison.OrdinalIgnoreCase) == true)
            {
                var seasonDirName = Path.GetFileName(sourceDir);
                if (seasonDirName?.StartsWith("Season", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var seriesSourceDir = Path.GetDirectoryName(sourceDir);
                    var seriesTargetDir = Path.GetDirectoryName(targetDir);
                    
                    if (!string.IsNullOrWhiteSpace(seriesSourceDir) && 
                        !string.IsNullOrWhiteSpace(seriesTargetDir) && 
                        IsLikelyPrivateFolder(seriesSourceDir))
                    {
                        // Add tvshow.nfo and other series-level assets
                        var seriesAssets = new[] { "tvshow.nfo", "poster.jpg", "fanart.jpg", "banner.jpg", "logo.png", "clearlogo.png", "landscape.jpg" };
                        foreach (var asset in seriesAssets)
                        {
                            var sPath = Path.Combine(seriesSourceDir, asset);
                            if (File.Exists(sPath) && !associatedMoves.Any(m => PathEquals(m.SourcePath, sPath)))
                            {
                                associatedMoves.Add(new AssociatedFileMove
                                {
                                    SourcePath = sPath,
                                    TargetPath = Path.Combine(seriesTargetDir, asset)
                                });
                            }
                        }
                    }
                }
            }
        }

        return associatedMoves.ToArray();
    }

    private static void AddCommonAssets(string sourceDir, string targetDir, List<AssociatedFileMove> list)
    {
        var commonNames = new[] { "movie.nfo", "poster.jpg", "fanart.jpg", "logo.png", "folder.jpg", "landscape.jpg", "backdrop.jpg", "clearlogo.png" };
        foreach (var commonName in commonNames)
        {
            var sourcePath = Path.Combine(sourceDir, commonName);
            if (File.Exists(sourcePath) && !list.Any(m => PathEquals(m.SourcePath, sourcePath)))
            {
                list.Add(new AssociatedFileMove
                {
                    SourcePath = sourcePath,
                    TargetPath = Path.Combine(targetDir, commonName)
                });
            }
        }
    }

    private static bool IsLikelyPrivateFolder(string directoryPath)
    {
        try
        {
            var dirName = Path.GetFileName(directoryPath);
            if (string.IsNullOrWhiteSpace(dirName))
            {
                return false;
            }

            // Common TV subfolders are always considered private to the parent show's hierarchy.
            if (dirName.StartsWith("Season", StringComparison.OrdinalIgnoreCase)
                || dirName.Equals("Specials", StringComparison.OrdinalIgnoreCase)
                || dirName.Equals("Extras", StringComparison.OrdinalIgnoreCase)
                || dirName.Equals("Subs", StringComparison.OrdinalIgnoreCase)
                || dirName.Equals("Featurettes", StringComparison.OrdinalIgnoreCase)
                || dirName.Equals("Behind the Scenes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var videoExtensions = new HashSet<string>(new[] { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v" }, StringComparer.OrdinalIgnoreCase);
            var files = Directory.GetFiles(directoryPath);
            var videoFileCount = files.Count(f => videoExtensions.Contains(Path.GetExtension(f)));
            
            if (videoFileCount == 1)
            {
                return true;
            }

            if (videoFileCount == 0)
            {
                // Check if it contains Season folders, which suggests it's a private series folder
                return Directory.GetDirectories(directoryPath)
                    .Any(d => Path.GetFileName(d).StartsWith("Season", StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    internal static void MarkDuplicateTargetConflicts(IList<OrganizationPlanEntry> entries)
    {
        var duplicates = entries
            .Where(entry => entry.Action == "move" && !string.IsNullOrWhiteSpace(entry.TargetPath))
            .GroupBy(entry => entry.TargetPath!, PathComparison.Comparer)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToArray();

        foreach (var entry in duplicates)
        {
            entry.Action = "conflict";
            entry.Reason = "DuplicateTargetInPlan";
        }
    }

    internal static void ResolveTargetConflicts(IList<OrganizationPlanEntry> entries, string? targetConflictPolicy)
    {
        var policy = ParseTargetConflictPolicy(targetConflictPolicy);
        if (policy == TargetConflictPolicy.Fail)
        {
            MarkDuplicateTargetConflicts(entries);
            return;
        }

        var reservedTargets = new HashSet<string>(PathComparison.Comparer);
        foreach (var entry in entries)
        {
            if (entry.Action == "move" && !string.IsNullOrWhiteSpace(entry.TargetPath))
            {
                reservedTargets.Add(entry.TargetPath);
            }
        }

        var existingTargetConflicts = entries
            .Where(entry =>
                entry.Action == "conflict"
                && string.Equals(entry.Reason, "TargetAlreadyExists", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.TargetPath))
            .OrderBy(entry => entry.SourcePath, PathComparison.Comparer)
            .ThenBy(entry => entry.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var entry in existingTargetConflicts)
        {
            if (policy == TargetConflictPolicy.Skip)
            {
                entry.Action = "skip";
                entry.Reason = "TargetAlreadyExists";
                continue;
            }

            if (TryGetSuffixedTargetPath(entry.SourcePath!, entry.TargetPath!, reservedTargets, out var suffixedPath))
            {
                entry.TargetPath = suffixedPath;
                entry.Action = "move";
                entry.Reason = "PlannedWithSuffix";
                reservedTargets.Add(suffixedPath!);
                continue;
            }

            entry.Action = "conflict";
            entry.Reason = "UnableToResolveTargetSuffix";
        }

        var duplicateGroups = entries
            .Where(entry => entry.Action == "move" && !string.IsNullOrWhiteSpace(entry.TargetPath))
            .GroupBy(entry => entry.TargetPath!, PathComparison.Comparer)
            .Where(group => group.Count() > 1)
            .ToArray();

        foreach (var group in duplicateGroups)
        {
            var ordered = group
                .OrderBy(entry => entry.SourcePath, PathComparison.Comparer)
                .ThenBy(entry => entry.ItemId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var keeper = ordered[0];

            foreach (var entry in ordered.Skip(1))
            {
                if (policy == TargetConflictPolicy.Skip)
                {
                    entry.Action = "skip";
                    entry.Reason = "DuplicateTargetInPlan";
                    continue;
                }

                if (TryGetSuffixedTargetPath(entry.SourcePath!, entry.TargetPath!, reservedTargets, out var suffixedPath))
                {
                    entry.TargetPath = suffixedPath;
                    entry.Action = "move";
                    entry.Reason = "PlannedWithSuffix";
                    reservedTargets.Add(suffixedPath!);
                    continue;
                }

                entry.Action = "conflict";
                entry.Reason = "UnableToResolveTargetSuffix";
            }

            // Preserve deterministic winner reason for duplicate groups.
            if (string.Equals(keeper.Reason, "DuplicateTargetInPlan", StringComparison.OrdinalIgnoreCase))
            {
                keeper.Reason = "Planned";
            }
        }
    }

    private static bool TryGetSuffixedTargetPath(
        string sourcePath,
        string targetPath,
        ISet<string> reservedTargets,
        out string? suffixedPath)
    {
        suffixedPath = null;

        var directory = Path.GetDirectoryName(targetPath);
        var extension = Path.GetExtension(targetPath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return false;
        }

        for (var index = 2; index <= 9999; index++)
        {
            var candidate = Path.Combine(directory, $"{fileNameWithoutExtension} ({index}){extension}");
            if (PathEquals(sourcePath, candidate))
            {
                continue;
            }

            if (File.Exists(candidate))
            {
                continue;
            }

            if (reservedTargets.Contains(candidate))
            {
                continue;
            }

            suffixedPath = candidate;
            return true;
        }

        return false;
    }

    private static TargetConflictPolicy ParseTargetConflictPolicy(string? targetConflictPolicy)
    {
        if (string.IsNullOrWhiteSpace(targetConflictPolicy))
        {
            return TargetConflictPolicy.Fail;
        }

        return targetConflictPolicy.Trim().ToLowerInvariant() switch
        {
            "fail" => TargetConflictPolicy.Fail,
            "skip" => TargetConflictPolicy.Skip,
            "suffix" => TargetConflictPolicy.Suffix,
            _ => TargetConflictPolicy.Fail
        };
    }

    private enum TargetConflictPolicy
    {
        Fail,
        Skip,
        Suffix
    }

    private static Dictionary<string, string> BuildMovieTemplateTokens(
        string title,
        int? year,
        ScanSuggestion suggestion)
    {
        var titleWithYear = year.HasValue ? $"{title} ({year.Value})" : title;
        var yearValue = year.HasValue ? year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = title,
            ["TitleWithYear"] = titleWithYear,
            ["Year"] = yearValue,
            ["Resolution"] = suggestion.Resolution ?? string.Empty,
            ["VideoCodec"] = suggestion.VideoCodec ?? string.Empty,
            ["VideoBitDepth"] = suggestion.VideoBitDepth ?? string.Empty,
            ["AudioCodec"] = suggestion.AudioCodec ?? string.Empty,
            ["AudioChannels"] = suggestion.AudioChannels ?? string.Empty,
            ["ReleaseGroup"] = suggestion.ReleaseGroup ?? string.Empty,
            ["MediaSource"] = suggestion.MediaSource ?? string.Empty,
            ["Edition"] = suggestion.Edition ?? string.Empty
        };

        return tokens;
    }

    private static Dictionary<string, string> BuildEpisodeTemplateTokens(
        string title,
        int season,
        int episode,
        ScanSuggestion suggestion)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = title,
            ["Season"] = season.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Season2"] = season.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            ["Episode"] = episode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Episode2"] = episode.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            ["Resolution"] = suggestion.Resolution ?? string.Empty,
            ["VideoCodec"] = suggestion.VideoCodec ?? string.Empty,
            ["VideoBitDepth"] = suggestion.VideoBitDepth ?? string.Empty,
            ["AudioCodec"] = suggestion.AudioCodec ?? string.Empty,
            ["AudioChannels"] = suggestion.AudioChannels ?? string.Empty,
            ["ReleaseGroup"] = suggestion.ReleaseGroup ?? string.Empty,
            ["MediaSource"] = suggestion.MediaSource ?? string.Empty,
            ["Edition"] = suggestion.Edition ?? string.Empty
        };

        return tokens;
    }

    private static bool TryRenderRelativePath(
        string template,
        IReadOnlyDictionary<string, string> tokens,
        bool normalizePathSegments,
        out string? relativePath)
    {
        relativePath = null;

        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        if (!TryResolveTemplateTokens(template, tokens, out var rendered))
        {
            return false;
        }

        var segments = rendered!
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => normalizePathSegments ? NormalizeSegment(segment) : segment.Trim().Trim('.'))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (segments.Length == 0)
        {
            return false;
        }

        relativePath = Path.Combine(segments);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        // Post-process to remove empty brackets/parentheses and double spaces
        // caused by missing tokens (e.g. "Movie (2024) []" -> "Movie (2024)")
        relativePath = relativePath.Replace("[]", string.Empty)
                                   .Replace("()", string.Empty)
                                   .Replace("[ ]", string.Empty)
                                   .Replace("( )", string.Empty);

        while (relativePath.Contains("  "))
        {
            relativePath = relativePath.Replace("  ", " ");
        }

        // Clean up any lingering spaces before dots or separators
        relativePath = relativePath.Replace(" .", ".")
                                   .Replace(" /", "/")
                                   .Replace(" \\", "\\")
                                   .Trim();

        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private static bool TryResolveTemplateTokens(
        string template,
        IReadOnlyDictionary<string, string> tokens,
        out string? resolved)
    {
        resolved = null;
        var builder = new StringBuilder(template.Length);

        for (var i = 0; i < template.Length;)
        {
            if (template[i] != '{')
            {
                builder.Append(template[i]);
                i++;
                continue;
            }

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                return false;
            }

            var token = template.Substring(i + 1, close - i - 1).Trim();
            if (string.IsNullOrWhiteSpace(token) || !tokens.TryGetValue(token, out var value))
            {
                return false;
            }

            builder.Append(value);
            i = close + 1;
        }

        resolved = builder.ToString();
        return true;
    }

    private static string BuildTargetPath(string rootPath, string relativePath, string extension)
    {
        var relativeWithExtension = relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : $"{relativePath}{extension}";
        return Path.Combine(rootPath, relativeWithExtension);
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            var leftFull = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rightFull = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return PathComparison.Equals(leftFull, rightFull);
        }
        catch
        {
            return PathComparison.Equals(left, right);
        }
    }
}
