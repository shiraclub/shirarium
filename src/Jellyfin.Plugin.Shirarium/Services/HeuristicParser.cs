using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Shirarium.Contracts;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// A high-performance, native C# heuristic parser for media filenames.
/// Ports the logic from the original Python engine to remove external dependencies.
/// </summary>
public sealed class HeuristicParser
{
    private static readonly HashSet<string> CommonJunk = new(StringComparer.OrdinalIgnoreCase)
    {
        "1080p", "720p", "2160p", "4k", "2k", "bluray", "brrip", "webrip", "webdl", "web", "x264", "x265",
        "h264", "h265", "aac", "dts", "hevc", "remux", "proper", "repack", "dual", "audio", "multi",
        "hdtv", "xvid", "divx", "ac3", "dts-hd", "truehd", "atmos", "unrated", "extended", "cut",
        "directors", "internal", "limited", "nf", "amzn", "dnp", "dsnp", "hmax", "hulu", "fr", "en", "jpn",
        "v2", "v3", "v4", "uhd", "hdr", "dovi", "dv", "hevc", "10bit", "8bit", "complete", "season", "pack"
    };

    // Regex patterns ported from Python
    private static readonly Regex SeasonEpisodeRe = new(
        @"(?:^|[\W_])(?:[sS](\d{1,2})[\W_]?[eE](\d{1,4})|(\d{1,2})x(\d{1,4})|[sS](\d{1,2}))(?:[\W_]?[eE](\d{1,4}))?(?:$|[\W_])",
        RegexOptions.Compiled);

    private static readonly Regex YearParenRe = new(@"[\(\[]((?:19|20)\d{2})[\)\]]", RegexOptions.Compiled);
    private static readonly Regex YearRe = new(@"(?:^|[\W_])((?:19|20)\d{2})(?:$|[\W_])", RegexOptions.Compiled);
    private static readonly Regex AbsoluteEpisodeRe = new(@"(?:^|[\W_])(?:- )?(?!19\d{2}|20\d{2})(\d{1,4})(?:$|[\W_])", RegexOptions.Compiled);
    
    private static readonly Regex QualRe = new(
        @"(?:^|[\W_])(1080p|720p|2160p|4k|bluray|web-?dl|brrip|webrip|hdtv|divx|xvid|dvdr|dvdrip)(?:$|[\W_])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VideoCodecRe = new(
        @"(?:^|[\W_])(x264|x265|h264|h265|hevc|av1|divx|xvid|mpeg2|vp9)(?:$|[\W_])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AudioCodecRe = new(
        @"(?:^|[\W_])(aac|ac3|dts(?:-hd)?|truehd|atmos|mp3|flac|eac3|vorbis|opus)(?:$|[\W_])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AudioChannelsRe = new(
        @"(?:^|[\W_])(2\.0|5\.1|7\.1)(?:$|[\W_])",
        RegexOptions.Compiled);

    private static readonly Regex ReleaseGroupRe = new(
        @"-([a-zA-Z0-9]+)(?:$|\.[a-zA-Z0-9]{2,4}$)",
        RegexOptions.Compiled);

    private static readonly Regex CrcRe = new(@"\[[0-9a-fA-F]{8}\]", RegexOptions.Compiled);
    private static readonly Regex LeadingTagRe = new(@"^[\[\({]([^}\)\]]+)[\]\)}]\s*", RegexOptions.Compiled);
    private static readonly Regex SplitRe = new(@"[.\-_()\[\]\s]+", RegexOptions.Compiled);
    private static readonly Regex StandaloneSeasonRe = new(@"[sS]\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses a filename using heuristic rules.
    /// </summary>
    public ParseFilenameResponse Parse(string path)
    {
        var p = Path.GetFileNameWithoutExtension(path);
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 1. Base Parse
        var result = ParseCore(p);

        // 2. Folder Context Enrichment
        if (parts.Length > 1)
        {
            // Traverse up to 2 levels up
            for (int i = parts.Length - 2; i >= 0 && i >= parts.Length - 3; i--)
            {
                var parentName = parts[i];
                if (string.IsNullOrWhiteSpace(parentName) || IsIgnoredFolder(parentName)) continue;

                var parentResult = ParseCore(parentName);

                // Merge logic
                if (result.Title == "Unknown Title" || result.Title.Length < 3 || int.TryParse(result.Title, out _))
                {
                    if (parentResult.Title != "Unknown Title")
                    {
                        result = result with { Title = parentResult.Title };
                    }
                }

                if (result.Year == null)
                {
                    result = result with { Year = parentResult.Year };
                }

                if (result.MediaType == "unknown")
                {
                    result = result with { MediaType = parentResult.MediaType };
                }

                if (result.Season == null) result = result with { Season = parentResult.Season };
                if (result.Episode == null) result = result with { Episode = parentResult.Episode };

                if (result.Title != "Unknown Title" && result.MediaType != "unknown") break;
            }
        }

        return result;
    }

    private static bool IsIgnoredFolder(string name)
    {
        return name.Equals("movies", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("tv", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("media", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("organized", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("incoming", StringComparison.OrdinalIgnoreCase);
    }

    private static ParseFilenameResponse ParseCore(string stem)
    {
        stem = CrcRe.Replace(stem, "");

        // Strip leading group tags
        while (true)
        {
            var match = LeadingTagRe.Match(stem);
            if (match.Success)
            {
                stem = stem.Substring(match.Index + match.Length);
            }
            else
            {
                break;
            }
        }

        int? season = null;
        int? episode = null;
        string mediaType = "unknown";
        double confidence = 0.4;
        string titleStem = stem;

        // Extract Metadata
        string? resolution = null;
        string? videoCodec = null;
        string? audioCodec = null;
        string? audioChannels = null;
        string? releaseGroup = null;

        var qualMatch = QualRe.Match(stem);
        if (qualMatch.Success) resolution = qualMatch.Groups[1].Value.ToLower();

        var vCodecMatch = VideoCodecRe.Match(stem);
        if (vCodecMatch.Success) videoCodec = vCodecMatch.Groups[1].Value.ToLower();

        var aCodecMatch = AudioCodecRe.Match(stem);
        if (aCodecMatch.Success) audioCodec = aCodecMatch.Groups[1].Value.ToLower();

        var channelMatch = AudioChannelsRe.Match(stem);
        if (channelMatch.Success) audioChannels = channelMatch.Groups[1].Value;

        var groupMatch = ReleaseGroupRe.Match(stem);
        if (groupMatch.Success) releaseGroup = groupMatch.Groups[1].Value;

        // Match Season/Episode
        var seMatch = SeasonEpisodeRe.Match(stem);
        if (seMatch.Success)
        {
            mediaType = "episode";
            confidence += 0.35;
            
            // Groups 1&2 (SxxExx), 3&4 (xxXxx), 5 (Sxx standalone)
            if (seMatch.Groups[1].Success && seMatch.Groups[2].Success)
            {
                season = int.Parse(seMatch.Groups[1].Value);
                episode = int.Parse(seMatch.Groups[2].Value);
            }
            else if (seMatch.Groups[3].Success && seMatch.Groups[4].Success)
            {
                season = int.Parse(seMatch.Groups[3].Value);
                episode = int.Parse(seMatch.Groups[4].Value);
            }
            else if (seMatch.Groups[5].Success)
            {
                season = int.Parse(seMatch.Groups[5].Value);
                // Episode remains null if only season is found
            }
            else
            {
                season = 1;
                episode = 1;
            }

            titleStem = stem.Substring(0, seMatch.Index);
        }
        else
        {
            var absMatch = AbsoluteEpisodeRe.Match(stem);
            if (absMatch.Success)
            {
                if (int.TryParse(absMatch.Groups[1].Value, out int num))
                {
                    bool isYearLike = num >= 1900 && num <= 2100;
                    bool hasKeywords = stem.Contains("season", StringComparison.OrdinalIgnoreCase) ||
                                       stem.Contains("ep", StringComparison.OrdinalIgnoreCase) ||
                                       stem.Contains("subs", StringComparison.OrdinalIgnoreCase) ||
                                       stem.Contains("raws", StringComparison.OrdinalIgnoreCase);

                    if (stem.Contains(" - ") || (!isYearLike && hasKeywords))
                    {
                        mediaType = "episode";
                        confidence += 0.25;
                        season = 1;
                        episode = num;
                        titleStem = stem.Substring(0, absMatch.Index);
                    }
                }
            }
        }

        // Match Year
        int? year = null;
        var yearParenMatch = YearParenRe.Match(stem);
        if (yearParenMatch.Success)
        {
            year = int.Parse(yearParenMatch.Groups[1].Value);
            if (mediaType == "unknown") mediaType = "movie";
            
            int idx = stem.IndexOf(yearParenMatch.Value, StringComparison.Ordinal);
            if (idx > 2) titleStem = stem.Substring(0, idx);
        }
        else
        {
            var yearMatch = YearRe.Match(stem);
            if (yearMatch.Success)
            {
                int foundYear = int.Parse(yearMatch.Groups[1].Value);
                string afterYear = stem.Substring(yearMatch.Index + yearMatch.Length).ToLowerInvariant();
                bool isFollowedByJunk = CommonJunk.Any(j => afterYear.Contains(j));
                bool isAtStart = yearMatch.Index < 2;

                if (isAtStart && !isFollowedByJunk && !stem.TrimEnd().EndsWith(foundYear.ToString()))
                {
                    // Likely a title starting with a number like "2012 (2009)" -> title is 2012
                    titleStem = stem;
                }
                else if (isFollowedByJunk || stem.TrimEnd().EndsWith(foundYear.ToString()))
                {
                    year = foundYear;
                    if (mediaType == "unknown") mediaType = "movie";
                    titleStem = stem.Substring(0, yearMatch.Index);
                }
                else
                {
                    if (mediaType == "unknown") mediaType = "movie";
                    year = foundYear;
                }
            }
        }

        // Final fallback for anime seasons (S4)
        if (mediaType == "unknown")
        {
            if (StandaloneSeasonRe.IsMatch(stem) || stem.Contains("season", StringComparison.OrdinalIgnoreCase))
            {
                mediaType = "episode";
            }
        }

        var tokens = SplitRe.Split(titleStem).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        string title = NormalizeTitleTokens(tokens);

        if (title.Contains(" - "))
        {
            var parts = title.Split(new[] { " - " }, StringSplitOptions.None);
            var lastPart = parts.Last();
            if (CommonJunk.Contains(lastPart) || QualRe.IsMatch(lastPart))
            {
                title = string.Join(" - ", parts.Take(parts.Length - 1)).Trim();
            }
        }

        if (mediaType == "movie" && year.HasValue)
        {
            confidence += 0.2;
        }

        return new ParseFilenameResponse
        {
            Title = title,
            MediaType = mediaType,
            Year = year,
            Season = season,
            Episode = episode,
            Confidence = Math.Min(Math.Round(confidence, 3), 1.0),
            Source = "heuristic",
            RawTokens = tokens,
            Resolution = resolution,
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            AudioChannels = audioChannels,
            ReleaseGroup = releaseGroup
        };
    }

    private static string NormalizeTitleTokens(List<string> tokens)
    {
        var cleaned = new List<string>();
        foreach (var token in tokens)
        {
            string lowToken = token.ToLowerInvariant();
            if (CommonJunk.Contains(lowToken)) break;
            
            // v2, x264 logic
            if (Regex.IsMatch(lowToken, @"^v\d+$") || lowToken == "x264" || lowToken == "x265" || lowToken == "h264" || lowToken == "h265")
            {
                break;
            }
            cleaned.Add(token);
        }

        if (cleaned.Count == 0) return "Unknown Title";

        string title = string.Join(" ", cleaned).Trim();
        
        // Simple Title Case
        TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
        title = textInfo.ToTitleCase(title.ToLower());

        return StripAccents(title);
    }

    private static string StripAccents(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c < 0x0300)
            {
                var normalizedString = c.ToString().Normalize(NormalizationForm.FormD);
                foreach (var ch in normalizedString)
                {
                    if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    {
                        sb.Append(ch);
                    }
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
