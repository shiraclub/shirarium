using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.IntegrationTests;

internal sealed class TestApplicationPaths : IApplicationPaths
{
    public string ProgramDataPath { get; init; } = string.Empty;

    public string ProgramSystemPath { get; init; } = string.Empty;

    public string CachePath { get; init; } = string.Empty;

    public string TempDirectory { get; init; } = string.Empty;

    public string PluginsPath { get; init; } = string.Empty;

    public string VirtualDataPath { get; init; } = string.Empty;

    public string DataPath { get; init; } = string.Empty;

    public string LogDirectoryPath { get; init; } = string.Empty;

    public string ConfigurationDirectoryPath { get; init; } = string.Empty;

    public string SystemConfigurationFilePath { get; init; } = string.Empty;

    public string WebPath { get; init; } = string.Empty;

    public string PluginConfigurationsPath { get; init; } = string.Empty;

    public string ImageCachePath { get; init; } = string.Empty;
}
