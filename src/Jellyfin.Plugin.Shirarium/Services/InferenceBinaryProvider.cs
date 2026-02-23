using System.Runtime.InteropServices;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Provides URLs for downloading the managed local LLM (llama.cpp) binary.
/// </summary>
public static class InferenceBinaryProvider
{
    /// <summary>
    /// Current targeted version of llama.cpp.
    /// </summary>
    public const string Version = "b8133";

    private const string BaseUrl = $"https://github.com/ggml-org/llama.cpp/releases/download/{Version}/";

    /// <summary>
    /// Gets the URL for the llama.cpp binary based on the operating system and architecture.
    /// </summary>
    /// <param name="osOverride">Optional OS override for testing.</param>
    /// <param name="archOverride">Optional Architecture override for testing.</param>
    /// <returns>The download URL or null if the platform is not supported.</returns>
    public static string? GetBinaryUrl(OSPlatform? osOverride = null, Architecture? archOverride = null)
    {
        var os = osOverride ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows :
                               RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux :
                               RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX :
                               OSPlatform.Create("UNKNOWN"));
        
        var arch = archOverride ?? RuntimeInformation.OSArchitecture;
        var isArm64 = arch == Architecture.Arm64;

        if (os == OSPlatform.Windows)
        {
            // Windows is almost always x64 for Jellyfin; default to Vulkan for GPU support
            return BaseUrl + $"llama-{Version}-bin-win-vulkan-x64.zip";
        }

        if (os == OSPlatform.Linux)
        {
            // Standard Ubuntu build for Linux
            return BaseUrl + $"llama-{Version}-bin-ubuntu-x64.tar.gz";
        }

        if (os == OSPlatform.OSX)
        {
            // macOS binaries are always tar.gz and split by Arch (Arm64 vs x64)
            string archStr = isArm64 ? "arm64" : "x64";
            return BaseUrl + $"llama-{Version}-bin-macos-{archStr}.tar.gz";
        }

        return null;
    }
}
