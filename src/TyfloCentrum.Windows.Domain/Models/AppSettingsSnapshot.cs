namespace TyfloCentrum.Windows.Domain.Models;

public sealed record AppSettingsSnapshot(
    string? PreferredInputDeviceId,
    string? PreferredOutputDeviceId,
    string? DownloadDirectoryPath,
    double DefaultPlaybackRate,
    bool RememberLastPlaybackRate,
    double? LastPlaybackRate,
    bool NotifyAboutNewPodcasts,
    bool NotifyAboutNewArticles
)
{
    public AppSettingsSnapshot Normalize()
    {
        return this with
        {
            PreferredInputDeviceId = NormalizeDeviceId(PreferredInputDeviceId),
            PreferredOutputDeviceId = NormalizeDeviceId(PreferredOutputDeviceId),
            DownloadDirectoryPath = NormalizePath(DownloadDirectoryPath),
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
        null,
        PlaybackRateCatalog.DefaultValue,
        false,
        null,
        true,
        true
    );

    private static string? NormalizeDeviceId(string? deviceId)
    {
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
    }
}
