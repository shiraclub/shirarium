using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class ScanLogic
{
    internal static HashSet<string> BuildExtensionSet(IEnumerable<string> extensions)
    {
        return new HashSet<string>(
            extensions
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
                .Select(ext => ext.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
    }

    internal static bool IsSupportedPath([NotNullWhen(true)] string? path, HashSet<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return extensions.Contains(extension);
    }

    internal static string[] GetCandidateReasons(object item)
    {
        var reasons = new List<string>();

        if (!HasAnyProviderIds(item))
        {
            reasons.Add("MissingProviderIds");
        }

        if (string.IsNullOrWhiteSpace(GetStringProperty(item, "Overview")))
        {
            reasons.Add("MissingOverview");
        }

        if (!HasValue(item, "ProductionYear"))
        {
            reasons.Add("MissingProductionYear");
        }

        return reasons.ToArray();
    }

    internal static string? GetResolution(object item)
    {
        var height = GetIntProperty(item, "Height");
        if (!height.HasValue)
        {
            return null;
        }

        if (height >= 2160) return "2160p";
        if (height >= 1440) return "1440p";
        if (height >= 1080) return "1080p";
        if (height >= 720) return "720p";
        if (height >= 480) return "480p";
        if (height >= 360) return "360p";
        return $"{height}p";
    }

    internal static string? GetVideoCodec(object item)
    {
        var mediaStreams = GetProperty(item, "MediaStreams") as IEnumerable;
        if (mediaStreams is null) return null;

        foreach (var stream in mediaStreams)
        {
            var type = GetStringProperty(stream!, "Type");
            if (string.Equals(type, "Video", StringComparison.OrdinalIgnoreCase))
            {
                var codec = GetStringProperty(stream!, "Codec");
                return codec?.ToUpperInvariant();
            }
        }
        return null;
    }

    internal static string? GetVideoBitDepth(object item)
    {
        var mediaStreams = GetProperty(item, "MediaStreams") as IEnumerable;
        if (mediaStreams is null) return null;

        foreach (var stream in mediaStreams)
        {
            var type = GetStringProperty(stream!, "Type");
            if (string.Equals(type, "Video", StringComparison.OrdinalIgnoreCase))
            {
                var bitDepth = GetIntProperty(stream!, "BitDepth");
                return bitDepth.HasValue ? $"{bitDepth}bit" : null;
            }
        }
        return null;
    }

    internal static string? GetAudioCodec(object item)
    {
        var mediaStreams = GetProperty(item, "MediaStreams") as IEnumerable;
        if (mediaStreams is null) return null;

        foreach (var stream in mediaStreams)
        {
            var type = GetStringProperty(stream!, "Type");
            if (string.Equals(type, "Audio", StringComparison.OrdinalIgnoreCase))
            {
                var codec = GetStringProperty(stream!, "Codec");
                return codec?.ToUpperInvariant();
            }
        }
        return null;
    }

    internal static string? GetAudioChannels(object item)
    {
        var mediaStreams = GetProperty(item, "MediaStreams") as IEnumerable;
        if (mediaStreams is null) return null;

        foreach (var stream in mediaStreams)
        {
            var type = GetStringProperty(stream!, "Type");
            if (string.Equals(type, "Audio", StringComparison.OrdinalIgnoreCase))
            {
                var channels = GetIntProperty(stream!, "Channels");
                if (!channels.HasValue) continue;

                if (channels == 6) return "5.1";
                if (channels == 8) return "7.1";
                if (channels == 2) return "2.0";
                if (channels == 1) return "1.0";
                return channels.ToString();
            }
        }
        return null;
    }

    internal static string? GetReleaseGroup(object item)
    {
        // Often stored in a tag or extra field, but we can try parsing the path if it's not explicitly in metadata.
        // For now, let's look at "Tags" property if available.
        var tags = GetProperty(item, "Tags") as string[];
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                // Heuristic: If it looks like a group (e.g., RARBG, YTS), return it.
                // This is a bit simplified.
                if (tag.Equals("RARBG", StringComparison.OrdinalIgnoreCase)) return "RARBG";
                if (tag.Equals("YTS", StringComparison.OrdinalIgnoreCase)) return "YTS";
            }
        }
        return null;
    }

    internal static string? GetMediaSource(object item)
    {
        var source = GetProperty(item, "SourceType");
        return source?.ToString();
    }

    internal static string? GetEdition(object item)
    {
        // Edition is often part of the name or a specific property in some versions of Jellyfin.
        // We'll look for common edition keywords in the name if a dedicated property isn't obvious.
        var name = GetStringProperty(item, "Name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (name.Contains("Extended", StringComparison.OrdinalIgnoreCase)) return "Extended Cut";
        if (name.Contains("Unrated", StringComparison.OrdinalIgnoreCase)) return "Unrated";
        if (name.Contains("Director's Cut", StringComparison.OrdinalIgnoreCase)) return "Director's Cut";
        return null;
    }

    internal static object? GetProperty(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        return property?.GetValue(item);
    }

    internal static int? GetIntProperty(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        var value = property?.GetValue(item);
        if (value is int intValue) return intValue;
        if (value is long longValue) return (int)longValue;
        return null;
    }

    internal static bool PassesConfidenceThreshold(double confidence, double minConfidence)
    {
        return confidence >= minConfidence;
    }

    internal static string GetPropertyAsString(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        var value = property?.GetValue(item);
        return value?.ToString() ?? string.Empty;
    }

    internal static string? GetStringProperty(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        return property?.GetValue(item) as string;
    }

    private static bool HasAnyProviderIds(object item)
    {
        var providerIds = item.GetType().GetProperty("ProviderIds")?.GetValue(item);
        if (providerIds is null)
        {
            return false;
        }

        if (providerIds is ICollection collection)
        {
            return collection.Count > 0;
        }

        if (providerIds is IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            return enumerator.MoveNext();
        }

        return false;
    }

    private static bool HasValue(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        if (property is null)
        {
            return false;
        }

        var value = property.GetValue(item);
        return value is not null;
    }
}
