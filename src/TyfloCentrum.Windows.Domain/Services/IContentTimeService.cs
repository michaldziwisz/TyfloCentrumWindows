using System.Text.Json;
using TyfloCentrum.Windows.Domain.Metadata;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IContentTimeService
{
    Task<IReadOnlyDictionary<ContentTimeKey, JsonElement?>> GetAsync(IEnumerable<ContentTimeKey> keys, CancellationToken cancellationToken = default);
}
