using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Notifications;

public sealed class ContentNotificationMonitor : IContentNotificationMonitor, IAsyncDisposable
{
    private const int NotificationFetchLimit = 10;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(15);

    private readonly IAppSettingsService _appSettingsService;
    private readonly IWordPressCatalogService _wordPressCatalogService;
    private readonly IContentNotificationStateStore _stateStore;
    private readonly IContentNotificationPresenter _notificationPresenter;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public ContentNotificationMonitor(
        IAppSettingsService appSettingsService,
        IWordPressCatalogService wordPressCatalogService,
        IContentNotificationStateStore stateStore,
        IContentNotificationPresenter notificationPresenter
    )
    {
        _appSettingsService = appSettingsService;
        _wordPressCatalogService = wordPressCatalogService;
        _stateStore = stateStore;
        _notificationPresenter = notificationPresenter;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loopTask is not null)
        {
            return Task.CompletedTask;
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = RunLoopAsync(_loopCts.Token);
        return Task.CompletedTask;
    }

    public async Task CheckNowAsync(CancellationToken cancellationToken = default)
    {
        await _checkLock.WaitAsync(cancellationToken);

        try
        {
            var settings = (await _appSettingsService.GetAsync(cancellationToken)).Normalize();
            var currentState = await _stateStore.GetAsync(cancellationToken);

            var podcastItemsTask = _wordPressCatalogService.GetItemsAsync(
                ContentSource.Podcast,
                NotificationFetchLimit,
                cancellationToken: cancellationToken
            );
            var articleItemsTask = _wordPressCatalogService.GetItemsAsync(
                ContentSource.Article,
                NotificationFetchLimit,
                cancellationToken: cancellationToken
            );

            await Task.WhenAll(podcastItemsTask, articleItemsTask);

            var podcastItems = podcastItemsTask.Result;
            var articleItems = articleItemsTask.Result;

            if (currentState.LastSeenPodcastPostId is int lastSeenPodcastId && settings.NotifyAboutNewPodcasts)
            {
                foreach (var item in GetNewItems(podcastItems, lastSeenPodcastId))
                {
                    await _notificationPresenter.ShowNewContentAsync(
                        ContentSource.Podcast,
                        item,
                        cancellationToken
                    );
                }
            }

            if (currentState.LastSeenArticlePostId is int lastSeenArticleId && settings.NotifyAboutNewArticles)
            {
                foreach (var item in GetNewItems(articleItems, lastSeenArticleId))
                {
                    await _notificationPresenter.ShowNewContentAsync(
                        ContentSource.Article,
                        item,
                        cancellationToken
                    );
                }
            }

            var nextState = new ContentNotificationStateSnapshot(
                podcastItems.FirstOrDefault()?.Id ?? currentState.LastSeenPodcastPostId,
                articleItems.FirstOrDefault()?.Id ?? currentState.LastSeenArticlePostId
            );

            await _stateStore.SaveAsync(nextState, cancellationToken);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_loopCts is null || _loopTask is null)
        {
            return;
        }

        _loopCts.Cancel();

        try
        {
            await _loopTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _loopCts.Dispose();
            _loopCts = null;
            _loopTask = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _checkLock.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CheckNowAsync(cancellationToken);

            using var timer = new PeriodicTimer(PollingInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await CheckNowAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static IReadOnlyList<WpPostSummary> GetNewItems(
        IReadOnlyList<WpPostSummary> items,
        int lastSeenPostId
    )
    {
        return items.Where(item => item.Id > lastSeenPostId).OrderBy(item => item.Id).ToArray();
    }
}
