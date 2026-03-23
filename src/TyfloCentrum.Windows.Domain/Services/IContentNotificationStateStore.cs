using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IContentNotificationStateStore
{
    Task<ContentNotificationStateSnapshot> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        ContentNotificationStateSnapshot state,
        CancellationToken cancellationToken = default
    );
}
