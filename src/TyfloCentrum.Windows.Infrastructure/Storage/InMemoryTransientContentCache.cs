using System.Collections.Concurrent;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Storage;

public sealed class InMemoryTransientContentCache : ITransientContentCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        if (
            _entries.TryGetValue(key, out var entry)
            && entry.ExpiresAtUtc > now
            && entry.Value is T cachedValue
        )
        {
            return cachedValue;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (
                _entries.TryGetValue(key, out entry)
                && entry.ExpiresAtUtc > now
                && entry.Value is T secondCachedValue
            )
            {
                return secondCachedValue;
            }

            var value = await factory(cancellationToken);
            _entries[key] = new CacheEntry(DateTimeOffset.UtcNow.Add(ttl), value!);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record CacheEntry(DateTimeOffset ExpiresAtUtc, object Value);
}
