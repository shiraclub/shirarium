using System;
using Jellyfin.Plugin.Shirarium.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Shirarium;

public sealed class Plugin : BasePlugin<PluginConfiguration>
{
    public static Plugin? Instance { get; private set; }

    public IApplicationPaths AppPaths { get; }

    public override string Name => "Shirarium";

    public override Guid Id => Guid.Parse("f8f6424f-3316-47ef-bfbe-b8138f7ef3ab");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        AppPaths = applicationPaths;
        Instance = this;
    }
}
