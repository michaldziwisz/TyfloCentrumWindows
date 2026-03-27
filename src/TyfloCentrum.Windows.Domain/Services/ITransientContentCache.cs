namespace TyfloCentrum.Windows.Domain.Services;

public interface ITransientContentCache
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default
    );
}
