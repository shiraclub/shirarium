using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Shirarium.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Shirarium;

/// <summary>
/// Main Jellyfin plugin entrypoint for Shirarium.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>
    , IHasWebPages
{
    /// <summary>
    /// Gets the singleton plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the Jellyfin application paths.
    /// </summary>
    public IApplicationPaths AppPaths { get; }

    /// <inheritdoc />
    public override string Name => "Shirarium";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("f8f6424f-3316-47ef-bfbe-b8138f7ef3ab");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Serializer used by Jellyfin plugin infrastructure.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        AppPaths = applicationPaths;
        Instance = this;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "Shirarium Review",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
