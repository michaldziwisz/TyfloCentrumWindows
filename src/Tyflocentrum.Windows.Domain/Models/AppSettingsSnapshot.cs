namespace Tyflocentrum.Windows.Domain.Models;

public sealed record AppSettingsSnapshot(
    string? PreferredInputDeviceId,
    string? PreferredOutputDeviceId,
    double DefaultPlaybackRate,
    bool RememberLastPlaybackRate,
    double? LastPlaybackRate
)
{
    public AppSettingsSnapshot Normalize()
    {
        return this with
        {
            PreferredInputDeviceId = NormalizeDeviceId(PreferredInputDeviceId),
            PreferredOutputDeviceId = NormalizeDeviceId(PreferredOutputDeviceId),
            DefaultPlaybackRate = PlaybackRateCatalog.Coerce(DefaultPlaybackRate),
            LastPlaybackRate = LastPlaybackRate is null
                ? null
                : PlaybackRateCatalog.Coerce(LastPlaybackRate.Value),
        };
    }

    public double EffectivePlaybackRate =>
        RememberLastPlaybackRate && LastPlaybackRate is double lastPlaybackRate
            ? PlaybackRateCatalog.Coerce(lastPlaybackRate)
            : PlaybackRateCatalog.Coerce(DefaultPlaybackRate);

    public static AppSettingsSnapshot Defaults { get; } = new(
        null,
        null,
        PlaybackRateCatalog.DefaultValue,
        false,
        null
    );

    private static string? NormalizeDeviceId(string? deviceId)
    {
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }
}
