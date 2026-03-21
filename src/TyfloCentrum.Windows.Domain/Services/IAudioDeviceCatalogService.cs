using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IAudioDeviceCatalogService
{
    Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
        CancellationToken cancellationToken = default
    );
}
