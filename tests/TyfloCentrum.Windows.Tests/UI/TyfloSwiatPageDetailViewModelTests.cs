using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class TyfloSwiatPageDetailViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_formats_page_content_for_reading()
    {
        var service = new FakeTyfloSwiatMagazineService
        {
            Page = new WpPostDetail
            {
                Id = 9001,
                Date = "2026-03-20T12:00:00",
                Link = "https://example.invalid/page/9001",
                Title = new RenderedText("Test &amp; strona"),
                Excerpt = new RenderedText(string.Empty),
                Content = new RenderedText("<h2>Nagłówek</h2><p>Treść strony</p>"),
                Guid = new RenderedText("https://example.invalid/?page_id=9001"),
            },
        };
        var favoritesService = new FakeFavoritesService();
        var viewModel = new TyfloSwiatPageDetailViewModel(
            service,
            new FakeExternalLinkLauncher(),
            favoritesService,
            new FakeShareService()
        );
        viewModel.Initialize(9001, "Fallback", "20.03.2026", "https://fallback.invalid");

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(9001, service.RequestedPageId);
        Assert.Equal("Test & strona", viewModel.Title);
        Assert.Contains("Nagłówek", viewModel.ContentText, StringComparison.Ordinal);
        Assert.Contains("Treść strony", viewModel.ContentText, StringComparison.Ordinal);
        Assert.False(viewModel.IsFavorite);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_adds_and_removes_tyflo_swiat_page()
    {
        var viewModel = new TyfloSwiatPageDetailViewModel(
            new FakeTyfloSwiatMagazineService
            {
                Page = new WpPostDetail
                {
                    Id = 9001,
                    Date = "2026-03-20T12:00:00",
                    Link = "https://example.invalid/page/9001",
                    Title = new RenderedText("Strona"),
                    Excerpt = new RenderedText(string.Empty),
                    Content = new RenderedText("<p>Treść</p>"),
                    Guid = new RenderedText("https://example.invalid/?page_id=9001"),
                },
            },
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new FakeShareService()
        );
        viewModel.Initialize(9001, "Strona", "20.03.2026", "https://example.invalid/page/9001");

        await viewModel.ToggleFavoriteAsync();
        Assert.True(viewModel.IsFavorite);

        await viewModel.ToggleFavoriteAsync();
        Assert.False(viewModel.IsFavorite);
    }

    [Fact]
    public async Task ShareAsync_invokes_system_share_for_tyflo_swiat_page()
    {
        var shareService = new FakeShareService();
        var viewModel = new TyfloSwiatPageDetailViewModel(
            new FakeTyfloSwiatMagazineService
            {
                Page = new WpPostDetail
                {
                    Id = 9001,
                    Date = "2026-03-20T12:00:00",
                    Link = "https://example.invalid/page/9001",
                    Title = new RenderedText("Strona"),
                    Excerpt = new RenderedText(string.Empty),
                    Content = new RenderedText("<p>Treść</p>"),
                    Guid = new RenderedText("https://example.invalid/?page_id=9001"),
                },
            },
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            shareService
        );
        viewModel.Initialize(9001, "Strona", "20.03.2026", "https://example.invalid/page/9001");

        await viewModel.ShareAsync();

        Assert.Equal("Strona", shareService.LastTitle);
        Assert.Equal("20.03.2026", shareService.LastDescription);
        Assert.Equal("https://example.invalid/page/9001", shareService.LastUrl);
        Assert.Null(viewModel.ErrorMessage);
    }

    private sealed class FakeTyfloSwiatMagazineService : ITyfloSwiatMagazineService
    {
        public required WpPostDetail Page { get; init; }

        public int RequestedPageId { get; private set; }

        public Task<IReadOnlyList<WpPostSummary>> GetIssuesAsync(
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TyfloSwiatIssueDetail> GetIssueAsync(
            int issueId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<WpPostDetail> GetPageAsync(
            int pageId,
            CancellationToken cancellationToken = default
        )
        {
            RequestedPageId = pageId;
            return Task.FromResult(Page);
        }
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        public Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class FakeFavoritesService : IFavoritesService
    {
        private readonly HashSet<string> _favoriteIds = [];

        public Task<IReadOnlyList<FavoriteItem>> GetItemsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<FavoriteItem>>([]);
        }

        public Task<bool> IsFavoriteAsync(string favoriteId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_favoriteIds.Contains(favoriteId));
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
                _favoriteIds.Contains(FavoriteItem.CreateId(source, postId, articleOrigin))
            );
        }

        public Task AddOrUpdateAsync(FavoriteItem item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _favoriteIds.Add(item.Id);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string favoriteId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _favoriteIds.Remove(favoriteId);
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
            _favoriteIds.Remove(FavoriteItem.CreateId(source, postId, articleOrigin));
            return Task.CompletedTask;
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
