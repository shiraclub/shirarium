using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium;

/// <summary>
/// Main Jellyfin plugin entrypoint for Shirarium.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>
    , IHasWebPages, IDisposable
{
    /// <summary>
    /// Gets the singleton plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the plugin logger.
    /// </summary>
    public static ILogger? Logger { get; private set; }

    /// <summary>
    /// Gets the Jellyfin application paths.
    /// </summary>
    public IApplicationPaths AppPaths { get; }

    /// <summary>
    /// Gets the managed inference manager.
    /// </summary>
    public InferenceManager? InferenceManager { get; private set; }

    /// <inheritdoc />
    public override string Name => "Shirarium";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("f8f6424f-3316-47ef-bfbe-b8138f7ef3ab");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Serializer used by Jellyfin plugin infrastructure.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILoggerFactory loggerFactory)
        : base(applicationPaths, xmlSerializer)
    {
        AppPaths = applicationPaths;
        Instance = this;
        Logger = loggerFactory.CreateLogger("Shirarium");
        InferenceManager = new InferenceManager(applicationPaths, loggerFactory.CreateLogger<InferenceManager>());
        _ = InferenceManager.StartAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "ShirariumDashboard",
                DisplayName = "Shirarium",
                EnableInMainMenu = true,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        InferenceManager?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        InferenceManager?.Dispose();
    }
}
