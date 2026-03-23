using System.Globalization;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Storage;

public sealed class LocalContentNotificationStateStore : IContentNotificationStateStore
{
    private const string LastSeenPodcastPostIdKey = "notifications.content.lastSeenPodcastPostId";
    private const string LastSeenArticlePostIdKey = "notifications.content.lastSeenArticlePostId";

    private readonly ILocalSettingsStore _localSettingsStore;

    public LocalContentNotificationStateStore(ILocalSettingsStore localSettingsStore)
    {
        _localSettingsStore = localSettingsStore;
    }

    public async Task<ContentNotificationStateSnapshot> GetAsync(
        CancellationToken cancellationToken = default
    )
    {
        var podcastTask = _localSettingsStore
            .GetStringAsync(LastSeenPodcastPostIdKey, cancellationToken)
            .AsTask();
        var articleTask = _localSettingsStore
            .GetStringAsync(LastSeenArticlePostIdKey, cancellationToken)
            .AsTask();

        await Task.WhenAll(podcastTask, articleTask);

        return new ContentNotificationStateSnapshot(
            ParseNullableInt(podcastTask.Result),
            ParseNullableInt(articleTask.Result)
        );
    }

    public async Task SaveAsync(
        ContentNotificationStateSnapshot state,
        CancellationToken cancellationToken = default
    )
    {
        await Task.WhenAll(
            _localSettingsStore
                .SetStringAsync(
                    LastSeenPodcastPostIdKey,
                    state.LastSeenPodcastPostId?.ToString(CultureInfo.InvariantCulture)
                        ?? string.Empty,
                    cancellationToken
                )
                .AsTask(),
            _localSettingsStore
                .SetStringAsync(
                    LastSeenArticlePostIdKey,
                    state.LastSeenArticlePostId?.ToString(CultureInfo.InvariantCulture)
                        ?? string.Empty,
                    cancellationToken
                )
                .AsTask()
        );
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
