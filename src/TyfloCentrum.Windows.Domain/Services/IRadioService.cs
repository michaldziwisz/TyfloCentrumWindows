using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IRadioService
{
    Uri LiveStreamUrl { get; }

    Task<RadioAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<RadioScheduleInfo> GetScheduleAsync(CancellationToken cancellationToken = default);
}
