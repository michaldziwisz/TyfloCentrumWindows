using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Options;
using TyfloCentrum.PushService.Services;
using TyfloCentrum.PushService.Tests.Support;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace TyfloCentrum.PushService.Tests.Services;

public sealed class WordPressPollingCoordinatorTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PollOnceAsync_initializes_baseline_without_dispatching_existing_posts()
    {
        Directory.CreateDirectory(_tempDirectory);

        var stateStore = CreateStateStore();
        var dispatcher = new PushNotificationDispatcher(
            stateStore,
            new FakeWnsNotificationSender(),
            NullLogger<PushNotificationDispatcher>.Instance
        );
        var client = new FakeWordPressFeedClient(
            podcasts:
            [
                new WordPressPostEnvelope(1, "2026-03-21", "https://podcasts/1", new WordPressRenderedText("Podcast 1")),
            ],
            articles:
            [
                new WordPressPostEnvelope(2, "2026-03-21", "https://articles/2", new WordPressRenderedText("Artykuł 2")),
            ]
        );

        var coordinator = new WordPressPollingCoordinator(
            stateStore,
            client,
            dispatcher,
            NullLogger<WordPressPollingCoordinator>.Instance
        );

        await coordinator.PollOnceAsync();

        var state = await stateStore.ReadAsync();

        Assert.True(state.PodcastBaselineInitialized);
        Assert.True(state.ArticleBaselineInitialized);
        Assert.Contains(1, state.SentPodcastIds);
        Assert.Contains(2, state.SentArticleIds);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private PushStateStore CreateStateStore()
    {
        var environment = new TestHostEnvironment(_tempDirectory);
        var options = Microsoft.Extensions.Options.Options.Create(
            new PushServiceOptions { DataDirectory = _tempDirectory }
        );
        return new PushStateStore(environment, options, NullLogger<PushStateStore>.Instance);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }

    private sealed class FakeWordPressFeedClient : WordPressFeedClient
    {
        private readonly IReadOnlyList<WordPressPostEnvelope> _podcasts;
        private readonly IReadOnlyList<WordPressPostEnvelope> _articles;

        public FakeWordPressFeedClient(
            IReadOnlyList<WordPressPostEnvelope> podcasts,
            IReadOnlyList<WordPressPostEnvelope> articles
        ) : base(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new PushServiceOptions())
        )
        {
            _podcasts = podcasts;
            _articles = articles;
        }

        public override Task<IReadOnlyList<WordPressPostEnvelope>> FetchLatestPodcastsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_podcasts);
        }

        public override Task<IReadOnlyList<WordPressPostEnvelope>> FetchLatestArticlesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_articles);
        }
    }
}
