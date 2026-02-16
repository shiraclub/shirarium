using System.Collections;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Source candidate provider backed by Jellyfin's library tree.
/// </summary>
internal sealed class JellyfinLibraryCandidateProvider : ISourceCandidateProvider
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinLibraryCandidateProvider"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    public JellyfinLibraryCandidateProvider(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public IEnumerable<object> GetCandidates(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EnumerateLibraryItems(_libraryManager.RootFolder).ToArray();
    }

    private static IEnumerable<object> EnumerateLibraryItems(object? rootFolder)
    {
        if (rootFolder is null)
        {
            yield break;
        }

        var type = rootFolder.GetType();
        var methods = type.GetMethods().Where(m => m.Name == "GetRecursiveChildren").ToArray();
        if (methods.Length == 0)
        {
            yield break;
        }

        foreach (var method in methods.OrderBy(m => m.GetParameters().Length))
        {
            var parameters = method.GetParameters();
            object? result;

            try
            {
                result = parameters.Length == 0
                    ? method.Invoke(rootFolder, null)
                    : method.Invoke(rootFolder, [null]);
            }
            catch
            {
                continue;
            }

            if (result is not IEnumerable enumerable)
            {
                continue;
            }

            foreach (var item in enumerable)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }

            yield break;
        }
    }
}

