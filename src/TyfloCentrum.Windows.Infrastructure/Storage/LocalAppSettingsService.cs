using System.Globalization;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Storage;

public sealed class LocalAppSettingsService : IAppSettingsService
{
    private const string PreferredInputDeviceIdKey = "settings.audio.inputDeviceId";
    private const string PreferredOutputDeviceIdKey = "settings.audio.outputDeviceId";
    private const string DownloadDirectoryPathKey = "settings.download.directoryPath";
    private const string DefaultPlaybackRateKey = "settings.playback.defaultRate";
    private const string RememberLastPlaybackRateKey = "settings.playback.rememberLastRate";
    private const string LastPlaybackRateKey = "settings.playback.lastRate";
    private const string RememberLastPlaybackVolumeKey = "settings.playback.rememberLastVolume";
    private const string LastPlaybackVolumePercentKey = "settings.playback.lastVolumePercent";
    private const string NotifyAboutNewPodcastsKey = "settings.notifications.newPodcasts";
    private const string NotifyAboutNewArticlesKey = "settings.notifications.newArticles";
    private const string ContentTypeAnnouncementPlacementKey =
        "settings.accessibility.contentTypeAnnouncementPlacement";

    private readonly ILocalSettingsStore _localSettingsStore;

    public LocalAppSettingsService(ILocalSettingsStore localSettingsStore)
    {
        _localSettingsStore = localSettingsStore;
    }

    public async Task<AppSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var preferredInputDeviceIdTask = _localSettingsStore
            .GetStringAsync(PreferredInputDeviceIdKey, cancellationToken)
            .AsTask();
        var preferredOutputDeviceIdTask = _localSettingsStore
            .GetStringAsync(PreferredOutputDeviceIdKey, cancellationToken)
            .AsTask();
        var downloadDirectoryPathTask = _localSettingsStore
            .GetStringAsync(DownloadDirectoryPathKey, cancellationToken)
            .AsTask();
        var defaultPlaybackRateTask = _localSettingsStore
            .GetStringAsync(DefaultPlaybackRateKey, cancellationToken)
            .AsTask();
        var rememberLastPlaybackRateTask = _localSettingsStore
            .GetStringAsync(RememberLastPlaybackRateKey, cancellationToken)
            .AsTask();
        var lastPlaybackRateTask = _localSettingsStore
            .GetStringAsync(LastPlaybackRateKey, cancellationToken)
            .AsTask();
        var rememberLastPlaybackVolumeTask = _localSettingsStore
            .GetStringAsync(RememberLastPlaybackVolumeKey, cancellationToken)
            .AsTask();
        var lastPlaybackVolumePercentTask = _localSettingsStore
            .GetStringAsync(LastPlaybackVolumePercentKey, cancellationToken)
            .AsTask();
        var notifyAboutNewPodcastsTask = _localSettingsStore
            .GetStringAsync(NotifyAboutNewPodcastsKey, cancellationToken)
            .AsTask();
        var notifyAboutNewArticlesTask = _localSettingsStore
            .GetStringAsync(NotifyAboutNewArticlesKey, cancellationToken)
            .AsTask();
        var contentTypeAnnouncementPlacementTask = _localSettingsStore
            .GetStringAsync(ContentTypeAnnouncementPlacementKey, cancellationToken)
            .AsTask();

        await Task.WhenAll(
            preferredInputDeviceIdTask,
            preferredOutputDeviceIdTask,
            downloadDirectoryPathTask,
            defaultPlaybackRateTask,
            rememberLastPlaybackRateTask,
            lastPlaybackRateTask,
            rememberLastPlaybackVolumeTask,
            lastPlaybackVolumePercentTask,
            notifyAboutNewPodcastsTask,
            notifyAboutNewArticlesTask,
            contentTypeAnnouncementPlacementTask
        );

        var snapshot = new AppSettingsSnapshot(
            NullIfWhiteSpace(preferredInputDeviceIdTask.Result),
            NullIfWhiteSpace(preferredOutputDeviceIdTask.Result),
            NullIfWhiteSpace(downloadDirectoryPathTask.Result),
            ParseDoubleOrDefault(defaultPlaybackRateTask.Result, PlaybackRateCatalog.DefaultValue),
            ParseBoolOrDefault(rememberLastPlaybackRateTask.Result, false),
            ParseNullableDouble(lastPlaybackRateTask.Result),
            ParseBoolOrDefault(notifyAboutNewPodcastsTask.Result, true),
            ParseBoolOrDefault(notifyAboutNewArticlesTask.Result, true),
            ParseBoolOrDefault(rememberLastPlaybackVolumeTask.Result, false),
            ParseNullableDouble(lastPlaybackVolumePercentTask.Result),
            ParseEnumOrDefault(
                contentTypeAnnouncementPlacementTask.Result,
                ContentTypeAnnouncementPlacement.None
            )
        );

        return snapshot.Normalize();
    }

    public async Task SaveAsync(
        AppSettingsSnapshot settings,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = settings.Normalize();

        var saveTasks = new Task[]
        {
            _localSettingsStore
                .SetStringAsync(
                    PreferredInputDeviceIdKey,
                    normalized.PreferredInputDeviceId ?? string.Empty,
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    PreferredOutputDeviceIdKey,
                    normalized.PreferredOutputDeviceId ?? string.Empty,
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    DownloadDirectoryPathKey,
                    normalized.DownloadDirectoryPath ?? string.Empty,
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    DefaultPlaybackRateKey,
                    normalized.DefaultPlaybackRate.ToString(CultureInfo.InvariantCulture),
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    RememberLastPlaybackRateKey,
                    normalized.RememberLastPlaybackRate.ToString(),
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    LastPlaybackRateKey,
                    normalized.LastPlaybackRate?.ToString(CultureInfo.InvariantCulture)
                        ?? string.Empty,
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    RememberLastPlaybackVolumeKey,
                    normalized.RememberLastPlaybackVolume.ToString(),
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    LastPlaybackVolumePercentKey,
                    normalized.LastPlaybackVolumePercent?.ToString(CultureInfo.InvariantCulture)
                        ?? string.Empty,
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    NotifyAboutNewPodcastsKey,
                    normalized.NotifyAboutNewPodcasts.ToString(),
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    NotifyAboutNewArticlesKey,
                    normalized.NotifyAboutNewArticles.ToString(),
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    ContentTypeAnnouncementPlacementKey,
                    normalized.ContentTypeAnnouncementPlacement.ToString(),
                    cancellationToken
                )
                .AsTask(),
        };

        await Task.WhenAll(saveTasks);
    }

    private static bool ParseBoolOrDefault(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static double ParseDoubleOrDefault(string? value, double defaultValue)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed
        )
            ? parsed
            : defaultValue;
    }

    private static double? ParseNullableDouble(string? value)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed
        )
            ? parsed
            : null;
    }

    private static TEnum ParseEnumOrDefault<TEnum>(string? value, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : defaultValue;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
