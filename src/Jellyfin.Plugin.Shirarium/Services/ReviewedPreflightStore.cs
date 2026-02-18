using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// File-based persistence helper for reviewed-preflight one-time tokens.
/// </summary>
public static class ReviewedPreflightStore
{
    private const int MaxEntries = 500;
    private static readonly TimeSpan DefaultTokenTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Result status for token-consume validation.
    /// </summary>
    public enum ConsumeStatus
    {
        /// <summary>
        /// Token is valid and consumed.
        /// </summary>
        Success,

        /// <summary>
        /// Token value was not supplied.
        /// </summary>
        MissingToken,

        /// <summary>
        /// Token was not found.
        /// </summary>
        TokenNotFound,

        /// <summary>
        /// Token exists but is expired.
        /// </summary>
        TokenExpired,

        /// <summary>
        /// Token plan fingerprint does not match.
        /// </summary>
        PlanFingerprintMismatch,

        /// <summary>
        /// Token selected-source hash does not match.
        /// </summary>
        SelectedSourceMismatch
    }

    /// <summary>
    /// Gets the reviewed-preflight token file path for the current Jellyfin data directory.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <returns>Absolute token file path.</returns>
    public static string GetFilePath(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "plugins", "Shirarium");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "reviewed-preflight-tokens.json");
    }

    /// <summary>
    /// Issues a new reviewed-preflight token.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="planFingerprint">Target plan fingerprint.</param>
    /// <param name="selectedSourcePaths">Selected source paths this token authorizes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The issued token entry.</returns>
    public static async Task<ReviewedPreflightEntry> IssueAsync(
        IApplicationPaths applicationPaths,
        string planFingerprint,
        IEnumerable<string> selectedSourcePaths,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedPaths = NormalizeSourcePaths(selectedSourcePaths);
        var entry = new ReviewedPreflightEntry
        {
            SchemaVersion = SnapshotSchemaVersions.ReviewedPreflight,
            Token = Guid.NewGuid().ToString("N"),
            IssuedAtUtc = now,
            ExpiresAtUtc = now.Add(DefaultTokenTtl),
            PlanFingerprint = planFingerprint,
            SelectedSourceHash = ComputeSelectedSourceHash(planFingerprint, normalizedPaths)
        };

        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.UpdateAsync(
            filePath,
            JsonOptions,
            static () => new ReviewedPreflightSnapshot(),
            snapshot =>
            {
                var entries = NormalizeEntries(snapshot, now).ToList();
                entries.Add(entry);
                if (entries.Count > MaxEntries)
                {
                    entries = entries
                        .OrderByDescending(candidate => candidate.IssuedAtUtc)
                        .Take(MaxEntries)
                        .OrderBy(candidate => candidate.IssuedAtUtc)
                        .ToList();
                }

                return new ReviewedPreflightSnapshot
                {
                    Entries = entries.ToArray()
                };
            },
            cancellationToken);

        return entry;
    }

    /// <summary>
    /// Validates and consumes a reviewed-preflight token.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="token">Token value.</param>
    /// <param name="planFingerprint">Expected plan fingerprint.</param>
    /// <param name="selectedSourcePaths">Expected selected source paths.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation status.</returns>
    public static async Task<ConsumeStatus> ConsumeIfValidAsync(
        IApplicationPaths applicationPaths,
        string token,
        string planFingerprint,
        IEnumerable<string> selectedSourcePaths,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ConsumeStatus.MissingToken;
        }

        var now = DateTimeOffset.UtcNow;
        var normalizedPaths = NormalizeSourcePaths(selectedSourcePaths);
        var selectedSourceHash = ComputeSelectedSourceHash(planFingerprint, normalizedPaths);
        var status = ConsumeStatus.TokenNotFound;

        var filePath = GetFilePath(applicationPaths);
        await StoreFileJson.UpdateAsync(
            filePath,
            JsonOptions,
            static () => new ReviewedPreflightSnapshot(),
            snapshot =>
            {
                var entries = NormalizeEntries(snapshot, now).ToList();
                var index = entries.FindIndex(candidate => candidate.Token.Equals(token, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    status = ConsumeStatus.TokenNotFound;
                    return new ReviewedPreflightSnapshot
                    {
                        Entries = entries.ToArray()
                    };
                }

                var entry = entries[index];
                entries.RemoveAt(index);

                if (entry.ExpiresAtUtc <= now)
                {
                    status = ConsumeStatus.TokenExpired;
                }
                else if (!entry.PlanFingerprint.Equals(planFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    status = ConsumeStatus.PlanFingerprintMismatch;
                }
                else if (!entry.SelectedSourceHash.Equals(selectedSourceHash, StringComparison.Ordinal))
                {
                    status = ConsumeStatus.SelectedSourceMismatch;
                }
                else
                {
                    status = ConsumeStatus.Success;
                }

                return new ReviewedPreflightSnapshot
                {
                    Entries = entries.ToArray()
                };
            },
            cancellationToken);

        return status;
    }

    internal static string ComputeSelectedSourceHash(string planFingerprint, IEnumerable<string> sourcePaths)
    {
        var normalizedPaths = NormalizeSourcePaths(sourcePaths);
        var payload = string.Format(
            CultureInfo.InvariantCulture,
            "{0}\n{1}",
            planFingerprint ?? string.Empty,
            string.Join('\n', normalizedPaths));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(digest);
    }

    private static ReviewedPreflightEntry[] NormalizeEntries(
        ReviewedPreflightSnapshot snapshot,
        DateTimeOffset now)
    {
        return snapshot.Entries
            .Where(entry => entry.SchemaVersion == SnapshotSchemaVersions.ReviewedPreflight)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Token))
            .Where(entry => entry.ExpiresAtUtc > now)
            .OrderBy(entry => entry.IssuedAtUtc)
            .ToArray();
    }

    private static string[] NormalizeSourcePaths(IEnumerable<string> sourcePaths)
    {
        return sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizePath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path
                .Trim()
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }
}
