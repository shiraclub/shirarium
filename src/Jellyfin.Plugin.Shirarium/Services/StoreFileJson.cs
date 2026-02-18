using System.Text.Json;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class StoreFileJson
{
    private static readonly TimeSpan LockAcquireTimeout = TimeSpan.FromSeconds(10);
    private const int LockRetryDelayMs = 50;

    internal static T ReadOrDefault<T>(
        string filePath,
        JsonSerializerOptions jsonOptions,
        Func<T> defaultFactory)
    {
        if (!File.Exists(filePath))
        {
            return defaultFactory();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? defaultFactory();
        }
        catch (JsonException ex)
        {
            if (TryBackupCorruptFile(filePath, out var backupPath))
            {
                LogWarning($"Detected invalid JSON in '{filePath}'. Backed up to '{backupPath}'. Error={ex.Message}");
            }
            else
            {
                LogWarning($"Detected invalid JSON in '{filePath}', but backup creation failed. Error={ex.Message}");
            }

            return defaultFactory();
        }
        catch
        {
            return defaultFactory();
        }
    }

    internal static async Task WriteAsync<T>(
        string filePath,
        T payload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        await using var lockHandle = await AcquireLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        await WriteWithoutLockAsync(filePath, payload, jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task UpdateAsync<T>(
        string filePath,
        JsonSerializerOptions jsonOptions,
        Func<T> defaultFactory,
        Func<T, T> update,
        CancellationToken cancellationToken = default)
    {
        await using var lockHandle = await AcquireLockAsync(filePath, cancellationToken).ConfigureAwait(false);
        var current = ReadOrDefault(filePath, jsonOptions, defaultFactory);
        var updated = update(current);
        await WriteWithoutLockAsync(filePath, updated, jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteWithoutLockAsync<T>(
        string filePath,
        T payload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var json = JsonSerializer.Serialize(payload, jsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task<FileStream> AcquireLockAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lockPath = filePath + ".lck";
        var startUtc = DateTimeOffset.UtcNow;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException)
            {
                var elapsed = DateTimeOffset.UtcNow - startUtc;
                if (elapsed >= LockAcquireTimeout)
                {
                    LogWarning($"Timed out acquiring store lock '{lockPath}' after {elapsed.TotalSeconds:0.0}s.");
                    throw new TimeoutException($"Timed out acquiring store lock for '{filePath}'.");
                }

                await Task.Delay(LockRetryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool TryBackupCorruptFile(string filePath, out string? backupPath)
    {
        backupPath = null;
        try
        {
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            var extension = Path.GetExtension(filePath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            backupPath = Path.Combine(directory, $"{fileNameWithoutExtension}.corrupt-{timestamp}{extension}");
            if (!File.Exists(backupPath))
            {
                File.Copy(filePath, backupPath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LogWarning(string message)
    {
        try
        {
            Console.Error.WriteLine($"[{DateTimeOffset.UtcNow:O}] [Shirarium.StoreFileJson] {message}");
        }
        catch
        {
        }
    }
}
