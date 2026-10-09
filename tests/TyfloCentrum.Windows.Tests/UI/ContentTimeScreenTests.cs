using System.Net;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using TyfloCentrum.Windows.Tests.Infrastructure;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;
using Xunit.Abstractions;
using static TyfloCentrum.Windows.Tests.Infrastructure.ContentTimeServiceTests;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContentTimeScreenTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("news", false)]
    [InlineData("news", true)]
    [InlineData("articles", false)]
    [InlineData("articles", true)]
    [InlineData("podcasts", false)]
    [InlineData("podcasts", true)]
    [InlineData("category", false)]
    [InlineData("category", true)]
    [InlineData("search", false)]
    [InlineData("search", true)]
    [InlineData("favorites", false)]
    [InlineData("favorites", true)]
    [InlineData("magazine", false)]
    [InlineData("magazine", true)]
    public async Task RealListsAreAvailableWhileMetadataIsBlockedAndUpdateInPlace(string screen, bool unavailable)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Transport(async (uri, ct) =>
        {
            if (uri.Host == "metadata.example" || Query(uri).ContainsKey("include"))
            {
                await release.Task.WaitAsync(ct);
                return unavailable ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Batch(uri);
            }
            return SourceResponse(uri);
        });
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options);
        var cache = new InMemoryTransientContentCache();
        var preferences = new ContentTypeAnnouncementPreferenceService();
        var actions = new Actions();
        var directory = Path.Combine(AppContext.BaseDirectory, "metadata-tests", Guid.NewGuid().ToString("N"));
        var favorites = new FileFavoritesService(Path.Combine(directory, "favorites.json"));
        try
        {
            ContentTimeItemViewModel time;
            object row;
            Func<object> currentRow;
            Func<Task> completion;
            Func<Task> action;
            if (screen == "news")
            {
                var vm = new NewsFeedViewModel(new WordPressNewsFeedService(http, Options, cache), actions, preferences, service);
                await vm.LoadIfNeededAsync().WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(vm.IsLoading);
                Assert.False(vm.HasError);
                var item = vm.Items.Single(i => i.Source == ContentSource.Article);
                row = item; time = item.Time; currentRow = () => vm.Items.Single(i => i.Source == ContentSource.Article);
                completion = () => vm.MetadataCompletion;
                action = () => vm.OpenItemAsync(item);
                Assert.True(vm.Items.Single(i => i.SupportsPlayback).SupportsPlayback);
            }
            else if (screen is "articles" or "podcasts" or "category")
            {
                ContentCatalogViewModelBase vm = screen == "podcasts" ?
                    new PodcastCatalogViewModel(new WordPressCatalogService(http, Options, cache), actions, preferences, service) :
                    new ArticleCatalogViewModel(new WordPressCatalogService(http, Options, cache), actions, preferences, service);
                await vm.LoadIfNeededAsync().WaitAsync(TimeSpan.FromSeconds(2));
                if (screen == "category") await vm.SelectCategoryAsync(vm.Categories.Single(c => c.Id == 9));
                Assert.False(vm.IsLoading);
                Assert.False(vm.HasError);
                var item = Assert.Single(vm.Items);
                row = item; time = item.Time; currentRow = () => vm.Items.Single();
                completion = () => vm.MetadataCompletion; action = () => vm.OpenItemAsync(item);
                Assert.Equal(screen == "podcasts", item.SupportsPlayback);
                if (screen == "category") Assert.Contains(handler.Urls, u => u.Query.Contains("categories=9"));
            }
            else if (screen == "search")
            {
                var vm = new SearchViewModel(new WordPressSearchService(http, Options, cache), actions, preferences, service) { SearchText = "Artykuł" };
                await vm.SearchAsync().WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(vm.IsLoading);
                Assert.False(vm.HasError);
                var item = vm.Results.Single(i => i.Source == ContentSource.Article);
                row = item; time = item.Time; currentRow = () => vm.Results.Single(i => i.Source == ContentSource.Article);
                completion = () => vm.MetadataCompletion; action = () => vm.OpenResultAsync(item);
            }
            else if (screen == "favorites")
            {
                await favorites.AddOrUpdateAsync(new FavoriteItem { Id = "Article:42", Source = ContentSource.Article, PostId = 42, Title = "Stary ulubiony", Link = "https://articles.example/42" });
                var vm = new FavoritesViewModel(favorites, actions, actions, actions, service);
                await vm.LoadIfNeededAsync().WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(vm.IsLoading);
                Assert.False(vm.HasError);
                var item = Assert.Single(vm.Items);
                row = item; time = item.Time; currentRow = () => vm.Items.Single();
                completion = () => vm.MetadataCompletion; action = async () => { Assert.True(await vm.OpenItemAsync(item)); };
            }
            else
            {
                var vm = new TyfloSwiatMagazineViewModel(new WordPressTyfloSwiatMagazineService(http, Options, cache), actions, favorites, preferences, service);
                var issue = new TyfloSwiatMagazineIssueItemViewModel(Post(70));
                await vm.SelectIssueAsync(issue).WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(vm.IsIssueLoading);
                Assert.False(vm.HasIssueError);
                var item = Assert.Single(vm.TocItems);
                row = item; time = item.Time; currentRow = () => vm.TocItems.Single();
                completion = () => vm.MetadataCompletion; action = async () => { Assert.True(await vm.OpenTocItemInBrowserAsync(item)); };
                Assert.DoesNotContain("Czytanie", issue.AccessibleLabel);
                Assert.Contains(handler.Urls, u => u.Host == "metadata.example" && u.Query.Contains("type=pages"));
                Assert.DoesNotContain(handler.Urls, u => u.Host == "metadata.example" && Query(u)["ids"].Split(',').Contains("70"));
            }
            if (screen != "podcasts") Assert.Equal("Czas niedostępny", time.Visible);
            await action();
            Assert.NotEmpty(actions.Opened);
            Assert.All(handler.Urls, uri =>
            {
                Assert.DoesNotContain(".mp3", uri.AbsoluteUri);
                Assert.DoesNotContain("pobierz", uri.AbsoluteUri);
                if (!uri.AbsolutePath.EndsWith("/pages/70")) Assert.DoesNotContain("content", uri.Query);
                Assert.False(uri.AbsolutePath.EndsWith("/posts/42") || uri.AbsolutePath.EndsWith("/pages/42"));
            });
            release.SetResult();
            await completion().WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Same(row, currentRow());
            Assert.Equal(screen == "podcasts" ? "Czas trwania: 3 s" : unavailable ? "Czas niedostępny" : "Czytanie: około 2 min", time.Visible);
            Assert.Equal(1, row.ToString()!.Split(time.Accessible).Length - 1);
            output.WriteLine(screen + (unavailable ? " 503" : " 200") + " URL_REGISTER=" + JsonSerializer.Serialize(handler.Urls));
        }
        finally
        {
            release.TrySetResult();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("[]")]
    [InlineData("\"broken\"")]
    [InlineData("{\"schema_version\":1,\"audio_status\":\"ready\",\"duration_seconds\":true}")]
    public async Task BrokenOptionalWpMetadataPreservesListsAndPlaybackAction(string raw)
    {
        using var handler = new Transport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"id\":42,\"date\":\"2026-10-08\",\"title\":{\"rendered\":\"Audycja\"},\"link\":\"https://example.test/42\",\"modified_gmt\":false,\"tyflocentrum\":" + raw + "}]")
        }));
        using var http = new HttpClient(handler);
        var posts = await new WordPressCatalogService(http, Options, new InMemoryTransientContentCache()).GetItemsAsync(ContentSource.Podcast, 20);
        var row = new ContentPostItemViewModel(ContentSource.Podcast, Assert.Single(posts));
        Assert.True(row.SupportsPlayback);
        Assert.Equal(42, row.PostId);
        Assert.Equal("Audycja", row.Title);
        Assert.Equal("Czas niedostępny", row.Time.Visible);
    }

    [Fact]
    public async Task FailedMetadataDoesNotBlockRealArticleDetails()
    {
        using var handler = new Transport((uri, _) => Task.FromResult(uri.Host == "metadata.example" ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : SourceResponse(uri)));
        using var http = new HttpClient(handler);
        var cache = new InMemoryTransientContentCache();
        var vm = new ArticleCatalogViewModel(new WordPressCatalogService(http, Options, cache), new Actions(), new(), new ContentTimeService(http, Options));
        await vm.LoadIfNeededAsync();
        await vm.MetadataCompletion;
        var row = Assert.Single(vm.Items);
        Assert.Equal("Czas niedostępny", row.Time.Visible);
        Assert.False(vm.HasError);
        var detail = await new WordPressPostDetailsService(http, Options, cache).GetPostAsync(ContentSource.Article, row.PostId);
        Assert.Contains("Czytelna treść", detail.Content.Rendered);
    }

    [Fact]
    public async Task StaleCallbackAfterRefreshCannotChangeNewRowsEvenIfProviderIgnoresCancellation()
    {
        var provider = new UncooperativeProvider();
        var enrich = new ContentTimeEnrichment(provider);
        var old = new ContentTimeItemViewModel(Key(42), null);
        enrich.Start([old]);
        var oldCompletion = enrich.Completion;
        var current = new ContentTimeItemViewModel(Key(42), null);
        enrich.Start([current]);
        provider.Responses[1].SetResult(new Dictionary<ContentTimeKey, JsonElement?> { [Key(42)] = Reading(42, minutes: 7) });
        await enrich.Completion;
        provider.Responses[0].SetResult(new Dictionary<ContentTimeKey, JsonElement?> { [Key(42)] = Reading(42, minutes: 1) });
        await oldCompletion;
        Assert.True(provider.Tokens[0].IsCancellationRequested);
        Assert.Equal("Czytanie: około 7 min", current.Visible);
        Assert.Equal("Czas niedostępny", old.Visible);
        enrich.Cancel();
    }

    [Fact]
    public async Task FavoriteMetadataRoundTripsAndLegacyItemsRemainInOrder()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "metadata-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "favorites.json");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, "[{\"id\":\"Article:1\",\"source\":1,\"postId\":1,\"title\":\"Stary\",\"savedAtUtc\":\"2026-10-08T12:00:00Z\"}]");
            var store = new FileFavoritesService(path);
            var legacy = Assert.Single(await store.GetItemsAsync());
            Assert.Equal("Stary", legacy.Title);
            var row = new ContentPostItemViewModel(ContentSource.Article, Post(42));
            row.Time.Apply(Reading(42));
            await new ContentFavoriteService(store).ToggleAsync(row);
            var reread = await new FileFavoritesService(path).GetItemsAsync();
            Assert.Equal(2, reread.Count);
            Assert.Contains(reread, i => i.Id == legacy.Id && i.SavedAtUtc == legacy.SavedAtUtc);
            var saved = reread.Single(i => i.PostId == 42);
            Assert.Equal("Czytanie: około 2 min", new FavoriteItemViewModel(saved).Time.Visible);
            Assert.Equal(reread.Select(i => i.Id), (await store.GetItemsAsync()).Select(i => i.Id));
            var malformed = saved with { ReadingMetadata = JsonSerializer.SerializeToElement(false) };
            await store.AddOrUpdateAsync(malformed);
            Assert.Equal(2, (await new FileFavoritesService(path).GetItemsAsync()).Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task FavoritesRefreshPostsPagesAndAudioButNotTopicsOrLinks()
    {
        using var handler = new Transport((uri, _) => Task.FromResult(Batch(uri)));
        using var http = new HttpClient(handler);
        var rows = new[]
        {
            new FavoriteItemViewModel(new FavoriteItem { Id = "Article:42", PostId = 42, Source = ContentSource.Article }),
            new FavoriteItemViewModel(new FavoriteItem { Id = "ArticlePage:42", PostId = 42, Source = ContentSource.Article, ArticleOrigin = FavoriteArticleOrigin.Page }),
            new FavoriteItemViewModel(new FavoriteItem { Id = "Podcast:42", PostId = 42, Source = ContentSource.Podcast }),
            new FavoriteItemViewModel(new FavoriteItem { Id = "Topic:42", PostId = 42, Kind = FavoriteKind.Topic }),
            new FavoriteItemViewModel(new FavoriteItem { Id = "Link:42", PostId = 42, Kind = FavoriteKind.Link })
        };
        var enrich = new ContentTimeEnrichment(new ContentTimeService(http, Options));
        enrich.Start(rows.Select(r => r.Time));
        await enrich.Completion;
        Assert.Equal("Czytanie: około 2 min", rows[0].Time.Visible);
        Assert.Equal("Czytanie: około 2 min", rows[1].Time.Visible);
        Assert.Equal("Czas trwania: 3 s", rows[2].Time.Visible);
        Assert.Empty(rows[3].Time.Visible);
        Assert.Empty(rows[4].Time.Visible);
        Assert.Equal(3, handler.Urls.Count);
        output.WriteLine("FAVORITES_URL_REGISTER=" + JsonSerializer.Serialize(handler.Urls));
        enrich.Cancel();
    }

    [Fact]
    public async Task ActualCatalogRefreshKeepsRowAndRejectsOlderMetadataForNewSourceRevision()
    {
        var revision = 0;
        using var handler = new Transport((uri, _) => Task.FromResult(uri.Host == "metadata.example" ? Batch(uri) :
            uri.AbsolutePath.EndsWith("categories") ? SourceResponse(uri) :
            Json(new[] { Post(42) with { ModifiedGmt = JsonSerializer.SerializeToElement(revision == 0 ? "2026-10-07T10:00:00" : "2026-10-08T10:00:00") } })));
        using var http = new HttpClient(handler);
        var vm = new ArticleCatalogViewModel(new WordPressCatalogService(http, Options, new NoCache()), new Actions(), new(), new ContentTimeService(http, Options));
        await vm.LoadIfNeededAsync();
        await vm.MetadataCompletion;
        var original = Assert.Single(vm.Items);
        Assert.Equal("Czytanie: około 2 min", original.Time.Visible);
        revision = 1;
        await vm.RefreshIfStaleAsync(TimeSpan.Zero);
        await vm.MetadataCompletion;
        Assert.Same(original, Assert.Single(vm.Items));
        Assert.Equal("Czas niedostępny", original.Time.Visible);
        Assert.False(vm.HasError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualSearchRefreshUpdatesSameRowsWithoutChangingSourceRevision(bool audio)
    {
        var minutes = 0;
        using var handler = new Transport((uri, _) => Task.FromResult(
            uri.Host == "metadata.example" ? (minutes == 0 ? Json(new { schema_version = 1, source = "tyfloswiat.pl", type = "posts", items = Array.Empty<object>() }) : Batch(uri, minutes: minutes)) :
            Json(new[] { Post(42) with { TimeMetadata = minutes == 0 ? null : JsonSerializer.SerializeToElement(new { schema_version = 1, audio_status = "ready", duration_seconds = minutes }) } })));
        using var http = new HttpClient(handler);
        var vm = new SearchViewModel(new WordPressSearchService(http, Options, new InMemoryTransientContentCache()), new Actions(), new(), new ContentTimeService(http, Options)) { SearchText = "Artykuł" };
        await vm.SearchAsync();
        await vm.MetadataCompletion;
        var row = vm.Results.Single(r => r.Source == (audio ? ContentSource.Podcast : ContentSource.Article));
        Assert.Equal("Czas niedostępny", row.Time.Visible);
        minutes = 7; // Jawna zmiana serwera PO pierwszym odczycie, PRZED akcją użytkownika.
        await vm.RefreshAsync();
        await vm.MetadataCompletion;
        Assert.Equal(audio ? "Czas trwania: 7 s" : "Czytanie: około 7 min", vm.Results.Single(r => r.Source == row.Source).Time.Visible);
        Assert.Same(row, vm.Results.Single(r => r.Source == row.Source));
        Assert.Equal(1, row.AccessibleLabel.Split(row.Time.Accessible).Length - 1);
        output.WriteLine($"PID={Environment.ProcessId}; ręczny RefreshAsync; {row.AccessibleLabel}");
    }

    [Fact]
    public async Task CatalogRefreshDoesNotEraseGoodReadingWhileNewBatchIsPending()
    {
        var ready = false;
        using var handler = new Transport((uri, _) => Task.FromResult(uri.Host == "metadata.example" ? Batch(uri, minutes: ready ? 7 : 2) : SourceResponse(uri)));
        using var http = new HttpClient(handler);
        var vm = new ArticleCatalogViewModel(new WordPressCatalogService(http, Options, new InMemoryTransientContentCache()), new Actions(), new(), new ContentTimeService(http, Options));
        await vm.LoadIfNeededAsync();
        await vm.MetadataCompletion;
        var row = Assert.Single(vm.Items);
        var seen = new List<string>();
        row.Time.PropertyChanged += (_, _) => seen.Add(row.Time.Visible);
        ready = true;
        await vm.RefreshIfStaleAsync(TimeSpan.Zero);
        await vm.MetadataCompletion;
        Assert.DoesNotContain("Czas niedostępny", seen);
        Assert.Equal("Czytanie: około 7 min", row.Time.Visible);
        Assert.Same(row, Assert.Single(vm.Items));
    }

    private sealed class NoCache : ITransientContentCache
    {
        public Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default) => factory(cancellationToken);
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal static WpPostSummary Post(int id) => new()
    {
        Id = id, Date = "2026-10-08T10:00:00", Title = new(id == 70 ? "Tyfloświat 1/2026" : "Artykuł"), Link = $"https://articles.example/{id}"
    };

    private static HttpResponseMessage SourceResponse(Uri uri)
    {
        if (uri.AbsolutePath.EndsWith("categories")) return Json(new[] { new { id = 9, name = "Kategoria", count = 1 } });
        if (uri.AbsolutePath.EndsWith("/pages/70") || uri.AbsolutePath.EndsWith("/posts/42")) return Json(new { id = 70, date = "2026-10-08T10:00:00", title = new { rendered = "Numer" }, link = "https://articles.example/70", content = new { rendered = "<p>Czytelna treść</p><a href='https://articles.example/42'>Artykuł</a>" } });
        var post = Post(42);
        if (uri.Host == "podcasts.example") post = post with { TimeMetadata = JsonSerializer.SerializeToElement(new { schema_version = 1, audio_status = "ready", duration_seconds = 2.5 }) };
        return Json(new[] { post });
    }

    private sealed class Actions : IExternalLinkLauncher, IClipboardService, IShareService
    {
        public List<string> Opened { get; } = [];
        public Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default) { Opened.Add(target); return Task.FromResult(true); }
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ShareLinkAsync(string title, string? description, string url, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class UncooperativeProvider : IContentTimeService
    {
        public List<TaskCompletionSource<IReadOnlyDictionary<ContentTimeKey, JsonElement?>>> Responses { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public Task<IReadOnlyDictionary<ContentTimeKey, JsonElement?>> GetAsync(IEnumerable<ContentTimeKey> keys, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            var response = new TaskCompletionSource<IReadOnlyDictionary<ContentTimeKey, JsonElement?>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Responses.Add(response);
            return response.Task;
        }
    }
}
