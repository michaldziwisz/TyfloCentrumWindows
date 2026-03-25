using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class FavoritesViewModelTests
{
    [Fact]
    public async Task ReloadAsync_filters_items_by_selected_source()
    {
        var service = new FakeFavoritesService();
        service.Items.AddRange(
        [
            new FavoriteItem
            {
                Id = FavoriteItem.CreateId(ContentSource.Podcast, 11),
                Kind = FavoriteKind.Podcast,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Podcast",
                PublishedDate = "19.03.2026",
                Link = "https://example.invalid/podcast/11",
                SavedAtUtc = new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero),
            },
            new FavoriteItem
            {
                Id = FavoriteItem.CreateId(ContentSource.Article, 22),
                Kind = FavoriteKind.Article,
                Source = ContentSource.Article,
                PostId = 22,
                Title = "Artykuł",
                PublishedDate = "20.03.2026",
                Link = "https://example.invalid/article/22",
                SavedAtUtc = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
            },
        ]);

        var viewModel = new FavoritesViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new FakeClipboardService(),
            new FakeShareService()
        );

        await viewModel.SelectFilterAsync(viewModel.Filters.Single(item => item.Kind == FavoriteKind.Podcast));

        var item = Assert.Single(viewModel.Items);
        Assert.Equal(FavoriteKind.Podcast, item.Kind);
        Assert.Equal("Masz 1 ulubioną pozycję.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ReloadAsync_filters_topic_items_without_mixing_other_favorites_from_same_podcast()
    {
        var service = new FakeFavoritesService();
        service.Items.AddRange(
        [
            new FavoriteItem
            {
                Id = FavoriteItem.CreateTopicId(11, "Temat 1", 123),
                Kind = FavoriteKind.Topic,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Temat 1",
                Subtitle = "Podcast 11",
                PublishedDate = "19.03.2026",
                StartPositionSeconds = 123,
                SavedAtUtc = new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
            },
            new FavoriteItem
            {
                Id = FavoriteItem.CreateLinkId(11, "https://example.invalid/link"),
                Kind = FavoriteKind.Link,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Link",
                Link = "https://example.invalid/link",
                SavedAtUtc = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
            },
        ]);

        var viewModel = new FavoritesViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new FakeClipboardService(),
            new FakeShareService()
        );

        await viewModel.SelectFilterAsync(viewModel.Filters.Single(item => item.Kind == FavoriteKind.Topic));

        var item = Assert.Single(viewModel.Items);
        Assert.Equal(FavoriteKind.Topic, item.Kind);
        Assert.Equal("Temat 1", item.Title);
    }

    [Fact]
    public async Task RemoveAsync_deletes_selected_item_and_sets_status()
    {
        var service = new FakeFavoritesService();
        service.Items.Add(new FavoriteItem
        {
            Id = FavoriteItem.CreateId(ContentSource.Podcast, 11),
            Kind = FavoriteKind.Podcast,
            Source = ContentSource.Podcast,
            PostId = 11,
            Title = "Podcast",
            PublishedDate = "19.03.2026",
            Link = "https://example.invalid/podcast/11",
            SavedAtUtc = new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero),
        });
        var viewModel = new FavoritesViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new FakeClipboardService(),
            new FakeShareService()
        );

        await viewModel.ReloadAsync();
        await viewModel.RemoveAsync(viewModel.Items.Single());

        Assert.Empty(viewModel.Items);
        Assert.Equal(FavoriteItem.CreateId(ContentSource.Podcast, 11), service.LastRemovedId);
        Assert.Equal("Usunięto z ulubionych: Podcast.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task CopyLinkAsync_copies_item_link_and_sets_status()
    {
        var service = new FakeFavoritesService();
        service.Items.Add(new FavoriteItem
        {
            Id = FavoriteItem.CreateLinkId(11, "https://example.invalid/link"),
            Kind = FavoriteKind.Link,
            Source = ContentSource.Podcast,
            PostId = 11,
            Title = "Link do materiału",
            Link = "https://example.invalid/link",
            SavedAtUtc = new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero),
        });
        var clipboardService = new FakeClipboardService();
        var viewModel = new FavoritesViewModel(
            service,
            new FakeExternalLinkLauncher(),
            clipboardService,
            new FakeShareService()
        );

        await viewModel.ReloadAsync();

        var copied = await viewModel.CopyLinkAsync(viewModel.Items.Single());

        Assert.True(copied);
        Assert.Equal("https://example.invalid/link", clipboardService.LastText);
        Assert.Equal("Skopiowano odnośnik: Link do materiału.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ShareItemAsync_invokes_system_share_for_favorite_link()
    {
        var service = new FakeFavoritesService();
        service.Items.Add(new FavoriteItem
        {
            Id = FavoriteItem.CreateLinkId(11, "https://example.invalid/link"),
            Kind = FavoriteKind.Link,
            Source = ContentSource.Podcast,
            PostId = 11,
            Title = "Link do materiału",
            Link = "https://example.invalid/link",
            ContextTitle = "Podcast testowy",
            SavedAtUtc = new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero),
        });
        var shareService = new FakeShareService();
        var viewModel = new FavoritesViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new FakeClipboardService(),
            shareService
        );

        await viewModel.ReloadAsync();

        var shared = await viewModel.ShareItemAsync(viewModel.Items.Single());

        Assert.True(shared);
        Assert.Equal("Link do materiału", shareService.LastTitle);
        Assert.Equal("Podcast testowy", shareService.LastDescription);
        Assert.Equal("https://example.invalid/link", shareService.LastUrl);
        Assert.Equal(
            "Otwarto systemowe udostępnianie dla: Link do materiału.",
            viewModel.StatusMessage
        );
    }

    private sealed class FakeFavoritesService : IFavoritesService
    {
        public List<FavoriteItem> Items { get; } = [];

        public string? LastRemovedId { get; private set; }

        public ContentSource? LastRemovedSource { get; private set; }

        public int LastRemovedPostId { get; private set; }

        public Task<IReadOnlyList<FavoriteItem>> GetItemsAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<FavoriteItem>>(
                Items.OrderByDescending(item => item.SavedAtUtc).ToArray()
            );
        }

        public Task<bool> IsFavoriteAsync(
            string favoriteId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Items.Any(item => item.Id == favoriteId));
        }

        public Task<bool> IsFavoriteAsync(
            ContentSource source,
            int postId,
            FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Items.Any(item => item.Id == FavoriteItem.CreateId(source, postId, articleOrigin))
            );
        }

        public Task AddOrUpdateAsync(FavoriteItem item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items.RemoveAll(candidate => candidate.Id == item.Id);
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string favoriteId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRemovedId = favoriteId;
            Items.RemoveAll(item => item.Id == favoriteId);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            ContentSource source,
            int postId,
            FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRemovedSource = source;
            LastRemovedPostId = postId;
            Items.RemoveAll(item => item.Id == FavoriteItem.CreateId(source, postId, articleOrigin));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        public Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastText = text;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeShareService : IShareService
    {
        public string? LastTitle { get; private set; }

        public string? LastDescription { get; private set; }

        public string? LastUrl { get; private set; }

        public Task<bool> ShareLinkAsync(
            string title,
            string? description,
            string url,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastTitle = title;
            LastDescription = description;
            LastUrl = url;
            return Task.FromResult(true);
        }
    }
}
