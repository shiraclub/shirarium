using System.Text;
using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanLogic
{
    internal const string DefaultMoviePathTemplate = "{TitleWithYear}/{TitleWithYear}";
    internal const string DefaultEpisodePathTemplate = "{Title}/Season {Season2}/{Title} - S{Season2}E{Episode2}";
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
            var movieTokens = BuildMovieTemplateTokens(title, suggestion.SuggestedYear);
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
                suggestion.SuggestedEpisode.Value);
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
        return entry;
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

    private static Dictionary<string, string> BuildMovieTemplateTokens(string title, int? year)
    {
        var titleWithYear = year.HasValue ? $"{title} ({year.Value})" : title;
        var yearValue = year.HasValue ? year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = title,
            ["TitleWithYear"] = titleWithYear,
            ["Year"] = yearValue
        };
    }

    private static Dictionary<string, string> BuildEpisodeTemplateTokens(
        string title,
        int season,
        int episode)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = title,
            ["Season"] = season.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Season2"] = season.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            ["Episode"] = episode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Episode2"] = episode.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
        };
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
