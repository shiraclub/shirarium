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
        catch (JsonException)
        {
            TryBackupCorruptFile(filePath);
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
                    throw new TimeoutException($"Timed out acquiring store lock for '{filePath}'.");
                }

                await Task.Delay(LockRetryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void TryBackupCorruptFile(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            var extension = Path.GetExtension(filePath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            var backupPath = Path.Combine(directory, $"{fileNameWithoutExtension}.corrupt-{timestamp}{extension}");
            if (!File.Exists(backupPath))
            {
                File.Copy(filePath, backupPath);
            }
        }
        catch
        {
        }
    }
}
