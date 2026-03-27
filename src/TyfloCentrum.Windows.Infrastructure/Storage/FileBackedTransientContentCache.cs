using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Storage;

public sealed class FileBackedTransientContentCache : ITransientContentCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly string _cacheDirectoryPath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MemoryCacheEntry> _memoryEntries = new(StringComparer.Ordinal);

    public FileBackedTransientContentCache()
    {
        _cacheDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TyfloCentrum.Windows",
            "http-cache"
        );
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var now = DateTimeOffset.UtcNow;
        if (TryGetFromMemory(key, now, out T? cachedValue))
        {
            return cachedValue!;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (TryGetFromMemory(key, now, out cachedValue))
            {
                return cachedValue!;
            }

            var diskEnvelope = await TryReadEnvelopeAsync(key, cancellationToken);
            if (diskEnvelope is not null && diskEnvelope.ExpiresAtUtc > now)
            {
                var diskValue = JsonSerializer.Deserialize<T>(diskEnvelope.PayloadJson, SerializerOptions);
                if (diskValue is not null)
                {
                    _memoryEntries[key] = new MemoryCacheEntry(diskEnvelope.ExpiresAtUtc, diskValue);
                    return diskValue;
                }
            }

            var value = await factory(cancellationToken);
            var expiresAtUtc = DateTimeOffset.UtcNow.Add(ttl);
            var payloadJson = JsonSerializer.Serialize(value, SerializerOptions);
            var envelope = new DiskCacheEnvelope(key, expiresAtUtc, payloadJson);

            _memoryEntries[key] = new MemoryCacheEntry(expiresAtUtc, value!);
            await WriteEnvelopeAsync(key, envelope, cancellationToken);

            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetFromMemory<T>(string key, DateTimeOffset now, out T value)
    {
        if (
            _memoryEntries.TryGetValue(key, out var entry)
            && entry.ExpiresAtUtc > now
            && entry.Value is T typedValue
        )
        {
            value = typedValue;
            return true;
        }

        _memoryEntries.TryRemove(key, out _);
        value = default!;
        return false;
    }

    private async Task<DiskCacheEnvelope?> TryReadEnvelopeAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        var path = GetCacheFilePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var envelope = await JsonSerializer.DeserializeAsync<DiskCacheEnvelope>(
                stream,
                SerializerOptions,
                cancellationToken
            );

            if (envelope is null)
            {
                return null;
            }

            if (envelope.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                TryDeleteFile(path);
                return null;
            }

            return envelope;
        }
        catch
        {
            TryDeleteFile(path);
            return null;
        }
    }

    private async Task WriteEnvelopeAsync(
        string key,
        DiskCacheEnvelope envelope,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(_cacheDirectoryPath);
        var path = GetCacheFilePath(key);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, envelope, SerializerOptions, cancellationToken);
    }

    private string GetCacheFilePath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_cacheDirectoryPath, $"{hash}.json");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private sealed record MemoryCacheEntry(DateTimeOffset ExpiresAtUtc, object Value);

    private sealed record DiskCacheEnvelope(
        string Key,
        DateTimeOffset ExpiresAtUtc,
        string PayloadJson
    );
}
