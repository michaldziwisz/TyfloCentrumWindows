using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IRadioService
{
    Uri LiveStreamUrl { get; }

    Task<RadioAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<RadioScheduleInfo> GetScheduleAsync(CancellationToken cancellationToken = default);
}
