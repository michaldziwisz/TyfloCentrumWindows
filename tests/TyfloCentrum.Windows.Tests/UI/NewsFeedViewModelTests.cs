using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class NewsFeedViewModelTests
{
    [Fact]
    public async Task LoadMoreAsync_appends_next_page_of_news_items()
    {
        var service = new FakeNewsFeedService
        {
            Pages =
            {
                [1] = new PagedResult<NewsFeedItem>(
                    [
                        new NewsFeedItem(
                            NewsItemKind.Podcast,
                            new WpPostSummary
                            {
                                Id = 1,
                                Date = "2026-03-18T08:00:00",
                                Link = "https://example.invalid/podcast-1",
                                Title = new RenderedText("Podcast pierwszy"),
                                Excerpt = new RenderedText("<p>Opis 1</p>"),
                            }
                        ),
                    ],
                    true
                ),
                [2] = new PagedResult<NewsFeedItem>(
                    [
                        new NewsFeedItem(
                            NewsItemKind.Article,
                            new WpPostSummary
                            {
                                Id = 2,
                                Date = "2026-03-17T08:00:00",
                                Link = "https://example.invalid/article-2",
                                Title = new RenderedText("Artykuł drugi"),
                                Excerpt = new RenderedText("<p>Opis 2</p>"),
                            }
                        ),
                    ],
                    false
                ),
            },
        };

        var viewModel = new NewsFeedViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        );

        await viewModel.LoadIfNeededAsync();
        await viewModel.LoadMoreAsync();

        Assert.Equal(2, viewModel.Items.Count);
        Assert.False(viewModel.HasMoreItems);
        Assert.Equal([1, 2], service.RequestedPages);
    }

    [Fact]
    public async Task RefreshIfStaleAsync_prepends_new_items_without_dropping_existing_ones()
    {
        var service = new FakeNewsFeedService
        {
            Pages =
            {
                [1] = new PagedResult<NewsFeedItem>(
                    [
                        CreateNewsItem(NewsItemKind.Podcast, 2, "Podcast drugi"),
                        CreateNewsItem(NewsItemKind.Article, 1, "Artykuł pierwszy"),
                    ],
                    true
                ),
            },
        };

        var viewModel = new NewsFeedViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        );

        await viewModel.LoadIfNeededAsync();

        service.Pages[1] = new PagedResult<NewsFeedItem>(
            [
                CreateNewsItem(NewsItemKind.Podcast, 4, "Podcast czwarty"),
                CreateNewsItem(NewsItemKind.Article, 3, "Artykuł trzeci"),
                CreateNewsItem(NewsItemKind.Podcast, 2, "Podcast drugi"),
                CreateNewsItem(NewsItemKind.Article, 1, "Artykuł pierwszy"),
            ],
            true
        );

        await viewModel.RefreshIfStaleAsync(TimeSpan.Zero);

        Assert.Equal([4, 3, 2, 1], viewModel.Items.Select(item => item.PostId));
        Assert.Equal([1, 1], service.RequestedPages);
    }

    private static NewsFeedItem CreateNewsItem(NewsItemKind kind, int id, string title)
    {
        return new NewsFeedItem(
            kind,
            new WpPostSummary
            {
                Id = id,
                Date = $"2026-03-{10 + id:00}T08:00:00",
                Link = $"https://example.invalid/{kind.ToString().ToLowerInvariant()}-{id}",
                Title = new RenderedText(title),
                Excerpt = new RenderedText($"<p>{title}</p>"),
            }
        );
    }

    private sealed class FakeNewsFeedService : INewsFeedService
    {
        public Dictionary<int, PagedResult<NewsFeedItem>> Pages { get; } = [];

        public List<int> RequestedPages { get; } = [];

        public Task<IReadOnlyList<NewsFeedItem>> GetLatestItemsAsync(
            int pageSize,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult<IReadOnlyList<NewsFeedItem>>(
                Pages.TryGetValue(1, out var page) ? page.Items : []
            );
        }

        public Task<PagedResult<NewsFeedItem>> GetLatestItemsPageAsync(
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken = default
        )
        {
            RequestedPages.Add(pageNumber);
            return Task.FromResult(
                Pages.TryGetValue(pageNumber, out var page)
                    ? page
                    : new PagedResult<NewsFeedItem>([], false)
            );
        }
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        public Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
