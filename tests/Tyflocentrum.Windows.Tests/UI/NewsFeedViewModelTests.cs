using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.UI.ViewModels;
using Xunit;

namespace Tyflocentrum.Windows.Tests.UI;

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

        var viewModel = new NewsFeedViewModel(service, new FakeExternalLinkLauncher());

        await viewModel.LoadIfNeededAsync();
        await viewModel.LoadMoreAsync();

        Assert.Equal(2, viewModel.Items.Count);
        Assert.False(viewModel.HasMoreItems);
        Assert.Equal([1, 2], service.RequestedPages);
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
