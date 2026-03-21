namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class AudioDeviceOptionViewModel
{
    public AudioDeviceOptionViewModel(string? deviceId, string label)
    {
        DeviceId = deviceId;
        Label = label;
    }

    public string? DeviceId { get; }

    public string Label { get; }
}
