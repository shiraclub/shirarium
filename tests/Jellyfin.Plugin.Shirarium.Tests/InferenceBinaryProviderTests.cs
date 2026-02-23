using System.Runtime.InteropServices;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class InferenceBinaryProviderTests
{
    private const string Version = InferenceBinaryProvider.Version;

    [Fact]
    public void GetBinaryUrl_Windows_ReturnsVulkanZip()
    {
        var url = InferenceBinaryProvider.GetBinaryUrl(OSPlatform.Windows, Architecture.X64);
        Assert.EndsWith($"/llama-{Version}-bin-win-vulkan-x64.zip", url);
    }

    [Fact]
    public void GetBinaryUrl_Linux_ReturnsTarGz()
    {
        var url = InferenceBinaryProvider.GetBinaryUrl(OSPlatform.Linux, Architecture.X64);
        Assert.EndsWith($"/llama-{Version}-bin-ubuntu-x64.tar.gz", url);
    }

    [Fact]
    public void GetBinaryUrl_MacOS_Arm64_ReturnsArm64TarGz()
    {
        var url = InferenceBinaryProvider.GetBinaryUrl(OSPlatform.OSX, Architecture.Arm64);
        Assert.EndsWith($"/llama-{Version}-bin-macos-arm64.tar.gz", url);
    }

    [Fact]
    public void GetBinaryUrl_MacOS_X64_ReturnsX64TarGz()
    {
        var url = InferenceBinaryProvider.GetBinaryUrl(OSPlatform.OSX, Architecture.X64);
        Assert.EndsWith($"/llama-{Version}-bin-macos-x64.tar.gz", url);
    }

    [Fact]
    public void GetBinaryUrl_UnknownOS_ReturnsNull()
    {
        var url = InferenceBinaryProvider.GetBinaryUrl(OSPlatform.Create("FreeBSD"), Architecture.X64);
        Assert.Null(url);
    }
}
