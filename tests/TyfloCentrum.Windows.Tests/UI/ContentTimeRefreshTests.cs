using System.ComponentModel;
using System.Net;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;
using Xunit.Abstractions;
using static TyfloCentrum.Windows.Tests.Infrastructure.ContentTimeServiceTests;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContentTimeRefreshTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("news")]
    [InlineData("articles")]
    [InlineData("article-category")]
    [InlineData("podcasts")]
    [InlineData("podcast-category")]
    [InlineData("search")]
    [InlineData("favorites")]
    [InlineData("magazine")]
    public async Task ScreenActionChangesTimesInSameObjectsWithUnchangedKeys(string screen)
    {
        var phase = 0;
        using var handler = new Transport((uri, _) => Task.FromResult(Response(uri, phase)));
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options);
        var cache = new InMemoryTransientContentCache();
        var actions = new Actions();
        var path = Path.Combine(AppContext.BaseDirectory, "refresh-tests", Guid.NewGuid().ToString("N"), "favorites.json");
        var store = new FileFavoritesService(path);
        try
        {
            var scenario = await CreateScreen(screen, http, service, cache, actions, store);
            await scenario.Completion();
            var originals = scenario.Rows();
            var times = originals.Select(Time).ToArray();
            var keys = times.Select(t => t.Key).ToArray();
            var names = new List<string>();
            foreach (var row in originals.Cast<INotifyPropertyChanged>())
                row.PropertyChanged += (_, args) => { if (args.PropertyName == "AccessibleLabel") names.Add(row.ToString()!); };
            Assert.All(times, t => Assert.Equal("Czas niedostępny", t.Visible));
            var before = handler.Urls.Count;
            await scenario.Resume();
            Assert.Equal(before, handler.Urls.Count); // Powrót przed progiem bez GET.
            foreach (var next in new[] { 1, 2, 2, -1, 3, 1, 4 })
            {
                phase = next; // Jedyny przełącznik odpowiedzi; nigdy liczba żądań.
                var notifications = names.Count;
                await scenario.Refresh();
                await scenario.Completion();
                var expectedAmount = next == 1 ? 7 : 9;
                foreach (var row in scenario.Rows())
                {
                    var time = Time(row);
                    Assert.Same(originals[Array.IndexOf(keys, time.Key)], row);
                    var expected = next is 3 or 4 ? "Czas niedostępny" : time.Key.Source == ContentSource.Podcast ? $"Czas trwania: {expectedAmount} s" : $"Czytanie: około {expectedAmount} min";
                    Assert.Equal(expected, time.Visible);
                    Assert.Equal(1, row.ToString()!.Split(time.Accessible).Length - 1);
                }
                Assert.Equal(keys, scenario.Rows().Select(Time).Select(t => t.Key));
                if (next == -1) Assert.Equal(notifications, names.Count); // Awaria zachowuje dane, bez fałszywego ready.
                output.WriteLine($"PID={Environment.ProcessId}; ekran={screen}; phase={next}; nazwy={JsonSerializer.Serialize(scenario.Rows().Select(r => r.ToString()))}");
            }
            var currentCount = handler.Urls.Count;
            await scenario.Resume();
            Assert.Equal(currentCount, handler.Urls.Count);
            Assert.All(handler.Urls, u => { Assert.DoesNotContain(".mp3", u.ToString()); Assert.DoesNotContain("pobierz", u.ToString()); });
            output.WriteLine("GET=" + JsonSerializer.Serialize(handler.Urls));
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task BackgroundAgeAndLongListAreBoundedAndDoNotRecreateRows()
    {
        var clock = new Clock();
        var phase = 0;
        using var handler = new Transport((uri, _) => Task.FromResult(Response(uri, phase)));
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options, clock, capacity: 32);
        var enrich = new ContentTimeEnrichment(service, clock);
        var rows = Enumerable.Range(1, 601).Select(id => new ContentTimeItemViewModel(Key(id), null, clock: clock)).ToArray();
        enrich.Start(rows);
        await enrich.Completion;
        Assert.Equal(13, handler.Urls.Count);
        phase = 1;
        clock.Now += TimeSpan.FromMinutes(5);
        await enrich.RefreshAsync(rows, manual: false);
        Assert.Equal(13, handler.Urls.Count);
        clock.Now += TimeSpan.FromMinutes(1);
        await enrich.RefreshAsync(rows, manual: false);
        Assert.Equal(26, handler.Urls.Count);
        Assert.All(rows, r => Assert.Equal("Czytanie: około 7 min", r.Visible));
        var changes = 0;
        foreach (var r in rows) r.PropertyChanged += (_, _) => changes++;
        await enrich.RefreshAsync(rows);
        Assert.Equal(39, handler.Urls.Count);
        Assert.Equal(0, changes);
        Assert.All(handler.Urls, u => Assert.InRange(Query(u)["ids"].Split(',').Length, 1, 50));
        enrich.Cancel();
    }

    [Fact]
    public async Task LateOldResponseCannotReplaceSameRowOrRefreshedCache()
    {
        var old = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var phase = 0;
        using var handler = new Transport((uri, _) => phase == 0 ? old.Task : Task.FromResult(Response(uri, phase)));
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options);
        var enrich = new ContentTimeEnrichment(service);
        var row = new ContentTimeItemViewModel(Key(42), null);
        enrich.Start([row]);
        var previous = enrich.Completion;
        phase = 2;
        await enrich.RefreshAsync([row]);
        old.SetResult(Response(handler.Urls.First(), 1));
        await previous;
        Assert.Equal("Czytanie: około 9 min", row.Visible);
        Assert.Equal(9, ContentTimePolicy.Reading((await service.GetAsync([Key(42)]))[Key(42)], null, DateTimeOffset.UtcNow)?.Amount);
        enrich.Cancel();
    }

    [Fact]
    public async Task RapidManualRequestsCoalesceButFinalServerStateWins()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var phase = 1;
        using var handler = new Transport(async (uri, ct) => { var captured = phase; await release.Task.WaitAsync(ct); return Response(uri, captured); });
        using var http = new HttpClient(handler);
        var enrich = new ContentTimeEnrichment(new ContentTimeService(http, Options));
        var row = new ContentTimeItemViewModel(Key(42), null);
        var first = enrich.RefreshAsync([row]);
        phase = 2;
        var pending = Enumerable.Range(0, 20).Select(_ => enrich.RefreshAsync([row])).ToArray();
        release.SetResult();
        await Task.WhenAll(pending.Append(first));
        Assert.Equal(2, handler.Urls.Count);
        Assert.Equal("Czytanie: około 9 min", row.Visible);
        enrich.Cancel();
    }

    [Fact]
    public async Task ManualRefreshHonorsRetryAfterAcrossKeysAndRecoversWithoutTimer()
    {
        var clock = new Clock();
        var fail = true;
        using var handler = new Transport((uri, _) =>
        {
            if (!fail) return Task.FromResult(Response(uri, 1));
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options, clock);
        await service.RefreshAsync([Key(42)]);
        fail = false;
        for (var id = 1; id <= 20; id++) Assert.Null((await service.RefreshAsync([Key(id)]))[Key(id)]);
        Assert.Single(handler.Urls);
        clock.Now += TimeSpan.FromMinutes(2);
        Assert.NotNull((await service.RefreshAsync([Key(42)]))[Key(42)]);
        Assert.Equal(2, handler.Urls.Count);
    }

    [Fact]
    public async Task CancellationThenManualReturnKeepsGoodRowsAndRecovers()
    {
        var block = false;
        using var handler = new Transport(async (uri, ct) => { if (block) await Task.Delay(Timeout.Infinite, ct); return Response(uri, 1); });
        using var http = new HttpClient(handler);
        var enrich = new ContentTimeEnrichment(new ContentTimeService(http, Options));
        var row = new ContentTimeItemViewModel(Key(42), null);
        await enrich.RefreshAsync([row]);
        block = true;
        using var cancel = new CancellationTokenSource();
        var pending = enrich.RefreshAsync([row], token: cancel.Token);
        cancel.Cancel();
        await pending;
        Assert.Equal("Czytanie: około 7 min", row.Visible);
        block = false;
        await enrich.RefreshAsync([row]);
        Assert.Equal("Czytanie: około 7 min", row.Visible);
        enrich.Cancel();
    }

    private static ContentTimeItemViewModel Time(object row) => row switch
    {
        NewsFeedItemViewModel r => r.Time, ContentPostItemViewModel r => r.Time,
        FavoriteItemViewModel r => r.Time, TyfloSwiatMagazineTocItemViewModel r => r.Time,
        _ => throw new ArgumentException(nameof(row))
    };
    private sealed record Screen(Func<object[]> Rows, Func<Task> Refresh, Func<Task> Resume, Func<Task> Completion);
    private static async Task<Screen> CreateScreen(string screen, HttpClient http, IContentTimeService service,
        ITransientContentCache cache, Actions actions, FileFavoritesService favorites)
    {
        if (screen == "news")
        {
            var vm = new NewsFeedViewModel(new WordPressNewsFeedService(http, Options, cache), actions, new(), service);
            await vm.LoadIfNeededAsync();
            return new(() => vm.Items.Cast<object>().ToArray(), () => vm.RefreshContentTimesAsync(), () => vm.RefreshContentTimesAsync(false), () => vm.MetadataCompletion);
        }
        if (screen is "articles" or "article-category" or "podcasts" or "podcast-category")
        {
            ContentCatalogViewModelBase vm = screen.StartsWith("podcast", StringComparison.Ordinal) ?
                new PodcastCatalogViewModel(new WordPressCatalogService(http, Options, cache), actions, new(), service) :
                new ArticleCatalogViewModel(new WordPressCatalogService(http, Options, cache), actions, new(), service);
            await vm.LoadIfNeededAsync();
            if (screen.EndsWith("category", StringComparison.Ordinal)) await vm.SelectCategoryAsync(vm.Categories.Single(c => c.Id == 9));
            return new(() => vm.Items.Cast<object>().ToArray(), () => vm.RefreshContentTimesAsync(), () => vm.RefreshContentTimesAsync(false), () => vm.MetadataCompletion);
        }
        if (screen == "search")
        {
            var vm = new SearchViewModel(new WordPressSearchService(http, Options, cache), actions, new(), service) { SearchText = "Artykuł" };
            await vm.SearchAsync();
            return new(() => vm.Results.Cast<object>().ToArray(), () => vm.RefreshAsync(), () => vm.RefreshContentTimesAsync(false), () => vm.MetadataCompletion);
        }
        if (screen == "favorites")
        {
            foreach (var item in new[] {
                new FavoriteItem { Id = "Podcast:42", Source = ContentSource.Podcast, PostId = 42, Title = "Audycja" },
                new FavoriteItem { Id = "Article:42", Source = ContentSource.Article, PostId = 42, Title = "Artykuł" },
                new FavoriteItem { Id = "ArticlePage:42", Source = ContentSource.Article, ArticleOrigin = FavoriteArticleOrigin.Page, PostId = 42, Title = "Strona" } })
                await favorites.AddOrUpdateAsync(item);
            var vm = new FavoritesViewModel(favorites, actions, actions, actions, service);
            await vm.LoadIfNeededAsync();
            return new(() => vm.Items.Cast<object>().ToArray(), () => vm.RefreshContentTimesAsync(), () => vm.RefreshContentTimesAsync(false), () => vm.MetadataCompletion);
        }
        var magazine = new TyfloSwiatMagazineViewModel(new WordPressTyfloSwiatMagazineService(http, Options, cache), actions, favorites, new(), service);
        await magazine.SelectIssueAsync(new(ContentTimeScreenTests.Post(70)));
        return new(() => magazine.TocItems.Cast<object>().ToArray(), () => magazine.RefreshContentTimesAsync(), () => magazine.RefreshContentTimesAsync(false), () => magazine.MetadataCompletion);
    }

    private static HttpResponseMessage Response(Uri uri, int phase)
    {
        var q = Query(uri);
        var amount = phase == 1 ? 7 : 9;
        object? audio = phase is 0 or 4 ? null : phase == 3 ? false : new { schema_version = 1, audio_status = "ready", duration_seconds = amount };
        if (uri.Host == "metadata.example" || q.ContainsKey("include"))
        {
            if (phase == -1) return new(HttpStatusCode.ServiceUnavailable);
            if (q.TryGetValue("include", out var include)) return Json(include.Split(',').Select(id => new { id = int.Parse(id), tyflocentrum = audio }));
            return Json(new { schema_version = 1, source = "tyfloswiat.pl", type = q["type"], items = phase is 0 or 4 ? Array.Empty<object>() : q["ids"].Split(',').Select(id => phase == 3 ? (object)new { id = int.Parse(id), tyflocentrum = false } : Reading(int.Parse(id), minutes: amount)).ToArray() });
        }
        if (uri.AbsolutePath.EndsWith("categories", StringComparison.Ordinal)) return Json(new[] { new { id = 9, name = "Kategoria", count = 1 } });
        if (uri.AbsolutePath.EndsWith("/pages/70", StringComparison.Ordinal)) return Json(new { id = 70, date = "2026-10-08T10:00:00", title = new { rendered = "Numer" }, link = "https://articles.example/70", content = new { rendered = "<a href='https://articles.example/42'>Artykuł</a>" } });
        return Json(new[] { ContentTimeScreenTests.Post(42) with { ModifiedGmt = JsonSerializer.SerializeToElement("2026-10-07T10:00:00"), TimeMetadata = JsonSerializer.SerializeToElement(audio) } });
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Actions : IExternalLinkLauncher, IClipboardService, IShareService
    {
        public Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ShareLinkAsync(string title, string? description, string url, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
