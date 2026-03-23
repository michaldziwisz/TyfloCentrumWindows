using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Notifications;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class ContentNotificationMonitorTests
{
    [Fact]
    public async Task First_check_primes_state_without_showing_notifications()
    {
        var catalogService = new FakeWordPressCatalogService();
        catalogService.SetItems(
            ContentSource.Podcast,
            [CreatePost(10, "Podcast 10"), CreatePost(9, "Podcast 9")]
        );
        catalogService.SetItems(
            ContentSource.Article,
            [CreatePost(30, "Artykuł 30"), CreatePost(29, "Artykuł 29")]
        );

        var presenter = new FakeContentNotificationPresenter();
        var monitor = CreateMonitor(catalogService, presenter);

        await monitor.CheckNowAsync();

        Assert.Empty(presenter.Notifications);
    }

    [Fact]
    public async Task Subsequent_check_shows_notifications_for_new_items()
    {
        var catalogService = new FakeWordPressCatalogService();
        var presenter = new FakeContentNotificationPresenter();
        var stateStore = new LocalContentNotificationStateStore(new InMemoryLocalSettingsStore());
        var monitor = CreateMonitor(catalogService, presenter, stateStore);

        catalogService.SetItems(
            ContentSource.Podcast,
            [CreatePost(10, "Podcast 10"), CreatePost(9, "Podcast 9")]
        );
        catalogService.SetItems(
            ContentSource.Article,
            [CreatePost(30, "Artykuł 30"), CreatePost(29, "Artykuł 29")]
        );
        await monitor.CheckNowAsync();

        catalogService.SetItems(
            ContentSource.Podcast,
            [CreatePost(12, "Podcast 12"), CreatePost(11, "Podcast 11"), CreatePost(10, "Podcast 10")]
        );
        catalogService.SetItems(
            ContentSource.Article,
            [CreatePost(31, "Artykuł 31"), CreatePost(30, "Artykuł 30")]
        );

        await monitor.CheckNowAsync();

        Assert.Collection(
            presenter.Notifications,
            item =>
            {
                Assert.Equal(ContentSource.Podcast, item.Source);
                Assert.Equal(11, item.PostId);
            },
            item =>
            {
                Assert.Equal(ContentSource.Podcast, item.Source);
                Assert.Equal(12, item.PostId);
            },
            item =>
            {
                Assert.Equal(ContentSource.Article, item.Source);
                Assert.Equal(31, item.PostId);
            }
        );
    }

    [Fact]
    public async Task Disabled_notifications_do_not_show_toasts_but_advance_state()
    {
        var catalogService = new FakeWordPressCatalogService();
        var presenter = new FakeContentNotificationPresenter();
        var settingsService = new FakeAppSettingsService
        {
            Snapshot = new AppSettingsSnapshot(
                null,
                null,
                null,
                PlaybackRateCatalog.DefaultValue,
                false,
                null,
                false,
                false
            ),
        };
        var stateStore = new LocalContentNotificationStateStore(new InMemoryLocalSettingsStore());
        var monitor = new ContentNotificationMonitor(
            settingsService,
            catalogService,
            stateStore,
            presenter
        );

        catalogService.SetItems(ContentSource.Podcast, [CreatePost(10, "Podcast 10")]);
        catalogService.SetItems(ContentSource.Article, [CreatePost(20, "Artykuł 20")]);
        await monitor.CheckNowAsync();

        catalogService.SetItems(ContentSource.Podcast, [CreatePost(11, "Podcast 11")]);
        catalogService.SetItems(ContentSource.Article, [CreatePost(21, "Artykuł 21")]);
        await monitor.CheckNowAsync();

        Assert.Empty(presenter.Notifications);

        var state = await stateStore.GetAsync();
        Assert.Equal(11, state.LastSeenPodcastPostId);
        Assert.Equal(21, state.LastSeenArticlePostId);
    }

    private static ContentNotificationMonitor CreateMonitor(
        FakeWordPressCatalogService catalogService,
        FakeContentNotificationPresenter presenter,
        IContentNotificationStateStore? stateStore = null
    )
    {
        return new ContentNotificationMonitor(
            new FakeAppSettingsService(),
            catalogService,
            stateStore ?? new LocalContentNotificationStateStore(new InMemoryLocalSettingsStore()),
            presenter
        );
    }

    private static WpPostSummary CreatePost(int id, string title)
    {
        return new WpPostSummary
        {
            Id = id,
            Date = "2026-03-21T12:00:00",
            Link = $"https://example.invalid/{id}",
            Title = new RenderedText(title),
            Excerpt = new RenderedText($"Opis {title}"),
        };
    }

    private sealed class FakeAppSettingsService : IAppSettingsService
    {
        public AppSettingsSnapshot Snapshot { get; set; } = AppSettingsSnapshot.Defaults;

        public Task<AppSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshot);
        }

        public Task SaveAsync(
            AppSettingsSnapshot settings,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            Snapshot = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWordPressCatalogService : IWordPressCatalogService
    {
        private readonly Dictionary<ContentSource, IReadOnlyList<WpPostSummary>> _entries = [];

        public void SetItems(ContentSource source, IReadOnlyList<WpPostSummary> items)
        {
            _entries[source] = items;
        }

        public Task<IReadOnlyList<WpCategorySummary>> GetCategoriesAsync(
            ContentSource source,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<WpCategorySummary>>([]);
        }

        public Task<IReadOnlyList<WpPostSummary>> GetItemsAsync(
            ContentSource source,
            int pageSize,
            int? categoryId = null,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries.TryGetValue(source, out var items);
            return Task.FromResult(items ?? []);
        }

        public Task<PagedResult<WpPostSummary>> GetItemsPageAsync(
            ContentSource source,
            int pageSize,
            int pageNumber,
            int? categoryId = null,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries.TryGetValue(source, out var items);
            return Task.FromResult(new PagedResult<WpPostSummary>(items ?? [], false));
        }
    }

    private sealed class FakeContentNotificationPresenter : IContentNotificationPresenter
    {
        public List<(ContentSource Source, int PostId)> Notifications { get; } = [];

        public Task ShowNewContentAsync(
            ContentSource source,
            WpPostSummary item,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notifications.Add((source, item.Id));
            return Task.CompletedTask;
        }
    }
}
