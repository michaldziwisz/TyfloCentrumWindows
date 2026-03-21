using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.App.Services;

public sealed class WindowsAudioDeviceCatalogService : IAudioDeviceCatalogService
{
    public async Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var devices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector());
        cancellationToken.ThrowIfCancellationRequested();
        return devices.Select(device => new AudioDeviceInfo(device.Id, device.Name)).ToArray();
    }

    public async Task<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var devices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
        cancellationToken.ThrowIfCancellationRequested();
        return devices.Select(device => new AudioDeviceInfo(device.Id, device.Name)).ToArray();
    }
}
