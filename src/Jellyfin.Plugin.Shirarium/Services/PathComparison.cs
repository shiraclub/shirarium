namespace Jellyfin.Plugin.Shirarium.Services;

internal static class PathComparison
{
    internal static StringComparer Comparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal static bool Equals(string? left, string? right)
    {
        return string.Equals(left, right, Comparison);
    }

    internal static bool StartsWith(string value, string prefix)
    {
        return value.StartsWith(prefix, Comparison);
    }
}
