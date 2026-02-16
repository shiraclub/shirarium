using System.Collections;

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

    internal static bool IsSupportedPath(string? path, HashSet<string> extensions)
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
