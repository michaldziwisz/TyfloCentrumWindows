namespace TyfloCentrum.Windows.Domain.Services;

public interface IContentNotificationMonitor
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task CheckNowAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
