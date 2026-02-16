using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class ScanLogicTests
{
    [Fact]
    public void BuildExtensionSet_NormalizesExtensions_AndIsCaseInsensitive()
    {
        var extensions = ScanLogic.BuildExtensionSet(new[] { "mkv", ".MP4", "", "  ", ".avi" });

        Assert.Contains(".mkv", extensions);
        Assert.Contains(".mp4", extensions);
        Assert.Contains(".avi", extensions);
        Assert.Equal(3, extensions.Count);
    }

    [Fact]
    public void IsSupportedPath_ReturnsTrue_ForConfiguredExtension()
    {
        var extensions = ScanLogic.BuildExtensionSet(new[] { ".mkv", ".mp4" });

        var supportedUpper = ScanLogic.IsSupportedPath(@"D:\Media\Movie.MKV", extensions);
        var supportedLower = ScanLogic.IsSupportedPath(@"D:\Media\Clip.mp4", extensions);
        var unsupported = ScanLogic.IsSupportedPath(@"D:\Media\Readme.txt", extensions);

        Assert.True(supportedUpper);
        Assert.True(supportedLower);
        Assert.False(unsupported);
    }

    [Fact]
    public void GetCandidateReasons_ReturnsEmpty_ForWellFormedItem()
    {
        var item = new FakeItem
        {
            Name = "Noroi",
            Overview = "A documentary-style horror film.",
            ProductionYear = 2005,
            ProviderIds = new Dictionary<string, string> { ["Tmdb"] = "1234" }
        };

        var reasons = ScanLogic.GetCandidateReasons(item);
        Assert.Empty(reasons);
    }

    [Fact]
    public void GetCandidateReasons_ReturnsExpectedFlags_ForMissingMetadata()
    {
        var item = new FakeItem
        {
            Name = "Unknown Clip",
            Overview = null,
            ProductionYear = null,
            ProviderIds = new Dictionary<string, string>()
        };

        var reasons = ScanLogic.GetCandidateReasons(item);

        Assert.Contains("MissingProviderIds", reasons);
        Assert.Contains("MissingOverview", reasons);
        Assert.Contains("MissingProductionYear", reasons);
        Assert.Equal(3, reasons.Length);
    }

    [Theory]
    [InlineData(0.55, 0.55, true)]
    [InlineData(0.80, 0.55, true)]
    [InlineData(0.54, 0.55, false)]
    public void PassesConfidenceThreshold_UsesInclusiveComparison(
        double confidence,
        double minConfidence,
        bool expected)
    {
        var actual = ScanLogic.PassesConfidenceThreshold(confidence, minConfidence);
        Assert.Equal(expected, actual);
    }

    private sealed class FakeItem
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        public string Name { get; init; } = "Unknown";

        public string? Path { get; init; } = @"D:\Media\Unknown.mkv";

        public string? Overview { get; init; }

        public int? ProductionYear { get; init; }

        public Dictionary<string, string> ProviderIds { get; init; } = new();
    }
}
