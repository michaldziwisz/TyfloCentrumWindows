using System.Threading;
using System.Threading.Tasks;

namespace Tyflocentrum.Windows.Domain.Services;

public interface ILocalSettingsStore
{
    ValueTask<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    ValueTask SetStringAsync(string key, string value, CancellationToken cancellationToken = default);

    ValueTask DeleteStringAsync(string key, CancellationToken cancellationToken = default);
}
