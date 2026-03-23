using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IPushNotificationRegistrationSyncService
{
    Task RegisterAsync(
        string token,
        string env,
        PushNotificationPreferences preferences,
        CancellationToken cancellationToken = default
    );

    Task UnregisterAsync(string token, CancellationToken cancellationToken = default);
}
