namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Abstraction for resolving media candidates that can be scanned by Shirarium.
/// </summary>
internal interface ISourceCandidateProvider
{
    /// <summary>
    /// Returns candidate items for scanning.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Candidate file paths.</returns>
    IEnumerable<string> GetCandidates(CancellationToken cancellationToken = default);
}

