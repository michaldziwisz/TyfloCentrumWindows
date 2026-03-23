using TyfloCentrum.PushService.Models;

namespace TyfloCentrum.PushService.Services;

public sealed class WordPressPollingCoordinator
{
    private readonly PushStateStore _stateStore;
    private readonly WordPressFeedClient _wordPressFeedClient;
    private readonly PushNotificationDispatcher _dispatcher;
    private readonly ILogger<WordPressPollingCoordinator> _logger;

    public WordPressPollingCoordinator(
        PushStateStore stateStore,
        WordPressFeedClient wordPressFeedClient,
        PushNotificationDispatcher dispatcher,
        ILogger<WordPressPollingCoordinator> logger
    )
    {
        _stateStore = stateStore;
        _wordPressFeedClient = wordPressFeedClient;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var fetchTask = _wordPressFeedClient.FetchLatestPodcastsAsync(cancellationToken);
        var articleTask = _wordPressFeedClient.FetchLatestArticlesAsync(cancellationToken);
        await Task.WhenAll(fetchTask, articleTask);

        await ProcessPostsAsync(
            PushCategories.Podcast,
            await fetchTask,
            baselineInitialized: state => state.PodcastBaselineInitialized,
            markBaselineInitialized: state => state.PodcastBaselineInitialized = true,
            getSentIds: state => state.SentPodcastIds,
            "Nowy podcast",
            cancellationToken
        );

        await ProcessPostsAsync(
            PushCategories.Article,
            await articleTask,
            baselineInitialized: state => state.ArticleBaselineInitialized,
            markBaselineInitialized: state => state.ArticleBaselineInitialized = true,
            getSentIds: state => state.SentArticleIds,
            "Nowy artykuł",
            cancellationToken
        );
    }

    public async Task DispatchEventAsync(
        string category,
        PushDispatchPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        var summary = await _dispatcher.DispatchAsync(category, payload, cancellationToken);
        _logger.LogInformation(
            "Dispatched event '{Category}' to {MatchedSubscribers} subscribers, delivered {DeliveredNotifications}, removed {RemovedSubscribers}.",
            category,
            summary.MatchedSubscribers,
            summary.DeliveredNotifications,
            summary.RemovedSubscribers
        );
    }

    private async Task ProcessPostsAsync(
        string category,
        IReadOnlyList<WordPressPostEnvelope> posts,
        Func<PushServiceState, bool> baselineInitialized,
        Action<PushServiceState> markBaselineInitialized,
        Func<PushServiceState, List<int>> getSentIds,
        string toastTitle,
        CancellationToken cancellationToken
    )
    {
        if (posts.Count == 0)
        {
            return;
        }

        var state = await _stateStore.ReadAsync(cancellationToken);
        var sentIds = new HashSet<int>(getSentIds(state));

        if (!baselineInitialized(state))
        {
            await _stateStore.UpdateAsync(
                currentState =>
                {
                    markBaselineInitialized(currentState);
                    var currentSentIds = getSentIds(currentState);
                    foreach (var post in posts)
                    {
                        AddId(currentSentIds, post.Id);
                    }
                },
                cancellationToken
            );

            _logger.LogInformation("Initialized {Category} push baseline with {Count} entries.", category, posts.Count);
            return;
        }

        var newPosts = posts.Where(post => !sentIds.Contains(post.Id)).Reverse().ToArray();
        if (newPosts.Length == 0)
        {
            return;
        }

        foreach (var post in newPosts)
        {
            var payload = new PushDispatchPayload(
                category,
                NormalizeRenderedText(post.Title.Rendered, toastTitle),
                "Otwórz TyfloCentrum, aby przejść do nowej treści.",
                post.Id,
                post.Date,
                post.Link
            );

            await _dispatcher.DispatchAsync(category, payload, cancellationToken);
        }

        await _stateStore.UpdateAsync(
            currentState =>
            {
                var currentSentIds = getSentIds(currentState);
                foreach (var post in newPosts)
                {
                    AddId(currentSentIds, post.Id);
                }
            },
            cancellationToken
        );
    }

    private static string NormalizeRenderedText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static void AddId(List<int> values, int id)
    {
        values.Remove(id);
        values.Insert(0, id);
        if (values.Count > 500)
        {
            values.RemoveRange(500, values.Count - 500);
        }
    }
}
