using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Scans the filesystem directly for media files, using Jellyfin's library roots as starting points.
/// </summary>
internal sealed class FilesystemCandidateProvider : ISourceCandidateProvider
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;

    public FilesystemCandidateProvider(ILibraryManager libraryManager, ILogger logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetCandidates(CancellationToken cancellationToken = default)
    {
        var virtualFolders = _libraryManager.GetVirtualFolders();
        var roots = virtualFolders.SelectMany(f => f.Locations).Distinct().ToArray();

        _logger.LogInformation("Scanning filesystem roots: {Roots}", string.Join(", ", roots));

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                _logger.LogWarning("Library root not found: {Path}", root);
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating files in {Path}", root);
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }
        }
    }
}
