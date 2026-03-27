namespace TyfloCentrum.Windows.Domain.Models;

public sealed record AppSettingsSnapshot(
    string? PreferredInputDeviceId,
    string? PreferredOutputDeviceId,
    string? DownloadDirectoryPath,
    double DefaultPlaybackRate,
    bool RememberLastPlaybackRate,
    double? LastPlaybackRate,
    bool NotifyAboutNewPodcasts,
    bool NotifyAboutNewArticles,
    bool RememberLastPlaybackVolume = false,
    double? LastPlaybackVolumePercent = null,
    ContentTypeAnnouncementPlacement ContentTypeAnnouncementPlacement =
        ContentTypeAnnouncementPlacement.None
)
{
    public const double DefaultPlaybackVolumePercent = 100d;

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
            LastPlaybackVolumePercent = LastPlaybackVolumePercent is null
                ? null
                : CoerceVolumePercent(LastPlaybackVolumePercent.Value),
            ContentTypeAnnouncementPlacement = NormalizeContentTypeAnnouncementPlacement(
                ContentTypeAnnouncementPlacement
            ),
        };
    }

    public double EffectivePlaybackRate =>
        RememberLastPlaybackRate && LastPlaybackRate is double lastPlaybackRate
            ? PlaybackRateCatalog.Coerce(lastPlaybackRate)
            : PlaybackRateCatalog.Coerce(DefaultPlaybackRate);

    public double EffectivePlaybackVolumePercent =>
        RememberLastPlaybackVolume && LastPlaybackVolumePercent is double lastPlaybackVolumePercent
            ? CoerceVolumePercent(lastPlaybackVolumePercent)
            : DefaultPlaybackVolumePercent;

    public static AppSettingsSnapshot Defaults { get; } = new(
        null,
        null,
        null,
        PlaybackRateCatalog.DefaultValue,
        false,
        null,
        true,
        true,
        false,
        null,
        ContentTypeAnnouncementPlacement.None
    );

    private static string? NormalizeDeviceId(string? deviceId)
    {
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
    }

    private static double CoerceVolumePercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return DefaultPlaybackVolumePercent;
        }

        return Math.Clamp(value, 0d, 100d);
    }

    private static ContentTypeAnnouncementPlacement NormalizeContentTypeAnnouncementPlacement(
        ContentTypeAnnouncementPlacement value
    )
    {
        return Enum.IsDefined(value) ? value : ContentTypeAnnouncementPlacement.None;
    }
}
