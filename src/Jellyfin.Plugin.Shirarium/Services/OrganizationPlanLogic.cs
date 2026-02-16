using System.Text;
using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanLogic
{
    internal static string NormalizeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "Unknown";
        }

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
            : suggestion.SuggestedTitle;
        title = string.IsNullOrWhiteSpace(title) ? "Unknown Title" : title;

        string? targetPath = null;
        if (string.Equals(suggestion.SuggestedMediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            entry.Strategy = "movie";

            var label = suggestion.SuggestedYear.HasValue
                ? $"{title} ({suggestion.SuggestedYear.Value})"
                : title;

            var folder = normalizePathSegments ? NormalizeSegment(label) : label;
            var fileName = normalizePathSegments ? NormalizeSegment(label) : label;

            targetPath = Path.Combine(rootPath, folder, $"{fileName}{ext}");
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

            var showFolder = normalizePathSegments ? NormalizeSegment(title) : title;
            var seasonFolder = $"Season {suggestion.SuggestedSeason.Value:00}";
            var episodeFile = $"{title} - S{suggestion.SuggestedSeason.Value:00}E{suggestion.SuggestedEpisode.Value:00}";
            if (normalizePathSegments)
            {
                episodeFile = NormalizeSegment(episodeFile);
            }

            targetPath = Path.Combine(rootPath, showFolder, seasonFolder, $"{episodeFile}{ext}");
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
            .GroupBy(entry => entry.TargetPath!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToArray();

        foreach (var entry in duplicates)
        {
            entry.Action = "conflict";
            entry.Reason = "DuplicateTargetInPlan";
        }
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            var leftFull = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rightFull = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(leftFull, rightFull, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
