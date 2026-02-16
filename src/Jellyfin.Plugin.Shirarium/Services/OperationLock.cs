using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Process-level file lock for serialization of apply/undo operations.
/// </summary>
public static class OperationLock
{
    /// <summary>
    /// Gets the lock file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute operation lock file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "apply.lock");
    }

    /// <summary>
    /// Attempts to acquire the operation lock.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Lock handle when successful; otherwise null.</returns>
    public static IDisposable? TryAcquire(IApplicationPaths applicationPaths)
    {
        var lockPath = GetFilePath(applicationPaths);

        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            return stream;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
