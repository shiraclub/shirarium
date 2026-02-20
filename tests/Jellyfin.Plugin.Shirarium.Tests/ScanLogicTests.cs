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
}
