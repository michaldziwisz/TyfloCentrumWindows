using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IAppSettingsService
{
    Task<AppSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettingsSnapshot settings, CancellationToken cancellationToken = default);
}
