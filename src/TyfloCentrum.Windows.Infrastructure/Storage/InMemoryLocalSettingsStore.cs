using System.Collections.Concurrent;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Storage;

public sealed class InMemoryLocalSettingsStore : ILocalSettingsStore
{
    private readonly ConcurrentDictionary<string, string> _entries = new(StringComparer.Ordinal);

    public ValueTask<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryGetValue(key, out var value);
        return ValueTask.FromResult<string?>(value);
    }

    public ValueTask SetStringAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteStringAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
