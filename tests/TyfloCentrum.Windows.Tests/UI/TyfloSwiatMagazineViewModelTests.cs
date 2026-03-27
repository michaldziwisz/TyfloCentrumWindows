using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class TyfloSwiatMagazineViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_groups_issues_by_year_without_opening_first_year()
    {
        var service = new FakeTyfloSwiatMagazineService
        {
            Issues =
            [
                CreateSummary(7771, "Tyfloświat 4/2025", "2025-12-20T12:00:00", "https://example.invalid/2025-4"),
                CreateSummary(7772, "Tyfloświat 1/2026", "2026-03-20T12:00:00", "https://example.invalid/2026-1"),
                CreateSummary(7773, "Tyfloświat 2/2026", "2026-06-20T12:00:00", "https://example.invalid/2026-2"),
            ],
            IssueDetails =
            {
                [7773] = new TyfloSwiatIssueDetail(
                    CreateDetail(7773, "Tyfloświat 2/2026", "2026-06-20T12:00:00", "https://example.invalid/2026-2", "<p>Treść numeru</p>"),
                    "https://example.invalid/2026-2.pdf",
                    [CreateSummary(9001, "Artykuł 1", "2026-06-20T10:00:00", "https://example.invalid/a1")]
                ),
            },
        };
        var viewModel = new TyfloSwiatMagazineViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new ContentTypeAnnouncementPreferenceService()
        );

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(2, viewModel.Years.Count);
        Assert.Equal(2026, viewModel.SelectedYear?.Year);
        Assert.Null(viewModel.OpenedYear);
        Assert.Null(viewModel.SelectedIssue);
        Assert.Empty(viewModel.Issues);
        Assert.Equal(string.Empty, viewModel.SelectedIssueTitle);
        Assert.Null(viewModel.SelectedIssuePdfUrl);
        Assert.Empty(viewModel.TocItems);
        Assert.Equal("Wczytano 3 numery czasopisma.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task OpenSelectedYearAsync_populates_issues_without_loading_detail()
    {
        var service = new FakeTyfloSwiatMagazineService
        {
            Issues =
            [
                CreateSummary(7771, "Tyfloświat 4/2025", "2025-12-20T12:00:00", "https://example.invalid/2025-4"),
                CreateSummary(7772, "Tyfloświat 1/2026", "2026-03-20T12:00:00", "https://example.invalid/2026-1"),
                CreateSummary(7773, "Tyfloświat 2/2026", "2026-06-20T12:00:00", "https://example.invalid/2026-2"),
            ],
        };
        var viewModel = new TyfloSwiatMagazineViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new ContentTypeAnnouncementPreferenceService()
        );

        await viewModel.LoadIfNeededAsync();
        await viewModel.OpenSelectedYearAsync(viewModel.SelectedYear);

        Assert.Equal(2026, viewModel.OpenedYear?.Year);
        Assert.Equal(2, viewModel.Issues.Count);
        Assert.Equal(7773, viewModel.SelectedIssue?.IssueId);
        Assert.Equal(string.Empty, viewModel.SelectedIssueTitle);
        Assert.Null(viewModel.SelectedIssuePdfUrl);
    }

    [Fact]
    public async Task OpenPdfAsync_sets_error_when_launcher_fails()
    {
        var service = new FakeTyfloSwiatMagazineService
        {
            Issues = [CreateSummary(7772, "Tyfloświat 1/2026", "2026-03-20T12:00:00", "https://example.invalid/2026-1")],
            IssueDetails =
            {
                [7772] = new TyfloSwiatIssueDetail(
                    CreateDetail(7772, "Tyfloświat 1/2026", "2026-03-20T12:00:00", "https://example.invalid/2026-1", "<p>Treść numeru</p>"),
                    "https://example.invalid/2026-1.pdf",
                    []
                ),
            },
        };
        var launcher = new FakeExternalLinkLauncher { Result = false };
        var viewModel = new TyfloSwiatMagazineViewModel(
            service,
            launcher,
            new FakeFavoritesService(),
            new ContentTypeAnnouncementPreferenceService()
        );
        await viewModel.LoadIfNeededAsync();
        await viewModel.OpenSelectedYearAsync(viewModel.SelectedYear);
        await viewModel.SelectIssueAsync(viewModel.SelectedIssue);

        var result = await viewModel.OpenPdfAsync();

        Assert.False(result);
        Assert.Equal("https://example.invalid/2026-1.pdf", launcher.LastTarget);
        Assert.Equal("Nie udało się otworzyć pliku PDF.", viewModel.IssueErrorMessage);
    }

    [Fact]
    public async Task ToggleTocFavoriteAsync_updates_item_state_and_status_message()
    {
        var service = new FakeTyfloSwiatMagazineService
        {
            Issues = [CreateSummary(7773, "Tyfloświat 2/2026", "2026-06-20T12:00:00", "https://example.invalid/2026-2")],
            IssueDetails =
            {
                [7773] = new TyfloSwiatIssueDetail(
                    CreateDetail(7773, "Tyfloświat 2/2026", "2026-06-20T12:00:00", "https://example.invalid/2026-2", "<p>Treść numeru</p>"),
                    "https://example.invalid/2026-2.pdf",
                    [CreateSummary(9001, "Artykuł 1", "2026-06-20T10:00:00", "https://example.invalid/a1")]
                ),
            },
        };
        var favoritesService = new FakeFavoritesService();
        var viewModel = new TyfloSwiatMagazineViewModel(
            service,
            new FakeExternalLinkLauncher(),
            favoritesService,
            new ContentTypeAnnouncementPreferenceService()
        );
        await viewModel.LoadIfNeededAsync();
        await viewModel.OpenSelectedYearAsync(viewModel.SelectedYear);
        await viewModel.SelectIssueAsync(viewModel.SelectedIssue);
        var item = Assert.Single(viewModel.TocItems);

        var added = await viewModel.ToggleTocFavoriteAsync(item);

        Assert.True(added);
        Assert.True(item.IsFavorite);
        Assert.Contains(item.PageId.ToString(), favoritesService.LastAddedId, StringComparison.Ordinal);
        Assert.Equal(
            "Dodano stronę TyfloŚwiata do ulubionych: Artykuł 1.",
            viewModel.StatusMessage
        );
    }

    [Fact]
    public async Task SelectIssueAsync_keeps_the_last_selected_issue_when_requests_overlap()
    {
        var service = new FakeTyfloSwiatMagazineService
        {
            Issues =
            [
                CreateSummary(7773, "Tyfloświat 2/2026", "2026-06-20T12:00:00", "https://example.invalid/2026-2"),
                CreateSummary(7772, "Tyfloświat 1/2026", "2026-03-20T12:00:00", "https://example.invalid/2026-1"),
            ],
            IssueDetails =
            {
                [7773] = new TyfloSwiatIssueDetail(
                    CreateDetail(7773, "Tyfloświat 2/2026", "2026-06-20T12:00:00", "https://example.invalid/2026-2", "<p>Treść numeru 2/2026</p>"),
                    "https://example.invalid/2026-2.pdf",
                    []
                ),
                [7772] = new TyfloSwiatIssueDetail(
                    CreateDetail(7772, "Tyfloświat 1/2026", "2026-03-20T12:00:00", "https://example.invalid/2026-1", "<p>Treść numeru 1/2026</p>"),
                    "https://example.invalid/2026-1.pdf",
                    []
                ),
            },
            IssueDelays =
            {
                [7772] = TimeSpan.FromMilliseconds(150),
            },
        };
        var viewModel = new TyfloSwiatMagazineViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new ContentTypeAnnouncementPreferenceService()
        );
        await viewModel.LoadIfNeededAsync();
        await viewModel.OpenSelectedYearAsync(viewModel.SelectedYear);

        var firstIssue = viewModel.Issues[0];
        var secondIssue = viewModel.Issues[1];

        var slowSelection = viewModel.SelectIssueAsync(secondIssue);
        var fastSelection = viewModel.SelectIssueAsync(firstIssue);
        await Task.WhenAll(slowSelection, fastSelection);

        Assert.Equal(firstIssue.IssueId, viewModel.SelectedIssue?.IssueId);
        Assert.Equal("Tyfloświat 2/2026", viewModel.SelectedIssueTitle);
        Assert.Equal("https://example.invalid/2026-2.pdf", viewModel.SelectedIssuePdfUrl);
    }

    private static WpPostSummary CreateSummary(int id, string title, string date, string link)
    {
        return new WpPostSummary
        {
            Id = id,
            Date = date,
            Link = link,
            Title = new RenderedText(title),
            Excerpt = new RenderedText(string.Empty),
        };
    }

    private static WpPostDetail CreateDetail(int id, string title, string date, string link, string content)
    {
        return new WpPostDetail
        {
            Id = id,
            Date = date,
            Link = link,
            Title = new RenderedText(title),
            Excerpt = new RenderedText(string.Empty),
            Content = new RenderedText(content),
            Guid = new RenderedText(link),
        };
    }

    private sealed class FakeTyfloSwiatMagazineService : ITyfloSwiatMagazineService
    {
        public IReadOnlyList<WpPostSummary> Issues { get; init; } = [];

        public Dictionary<int, TyfloSwiatIssueDetail> IssueDetails { get; init; } = [];

        public Dictionary<int, TimeSpan> IssueDelays { get; init; } = [];

        public Task<IReadOnlyList<WpPostSummary>> GetIssuesAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Issues);
        }

        public async Task<TyfloSwiatIssueDetail> GetIssueAsync(
            int issueId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IssueDelays.TryGetValue(issueId, out var delay) && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return IssueDetails[issueId];
        }

        public Task<WpPostDetail> GetPageAsync(
            int pageId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        public bool Result { get; init; } = true;

        public string? LastTarget { get; private set; }

        public Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastTarget = target;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeFavoritesService : IFavoritesService
    {
        private readonly HashSet<string> _favoriteIds = [];

        public string? LastAddedId { get; private set; }

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
            LastAddedId = item.Id;
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
}
