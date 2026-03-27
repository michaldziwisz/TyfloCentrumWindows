using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class SearchViewModelTests
{
    [Fact]
    public async Task SearchAsync_trims_query_and_loads_results_for_selected_scope()
    {
        var service = new FakeSearchService
        {
            Items =
            [
                new SearchResultItem(
                    ContentSource.Podcast,
                    new WpPostSummary
                    {
                        Id = 101,
                        Date = "2026-03-19T10:15:00",
                        Link = "https://example.invalid/post/101",
                        Title = new RenderedText("Podcast testowy"),
                        Excerpt = new RenderedText("<p>Krótki opis</p>"),
                    }
                ),
            ],
        };

        var viewModel = new SearchViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        )
        {
            SearchText = "  test  ",
            SelectedScope = SearchScopeOptionViewModel.All[1],
        };

        await viewModel.SearchAsync();

        Assert.Equal(SearchScope.Podcasts, service.RequestedScope);
        Assert.Equal("test", service.RequestedQuery);
        Assert.Single(viewModel.Results);
        Assert.Equal("Znaleziono 1 wynik.", viewModel.StatusMessage);
        Assert.Equal("test", viewModel.LastSearchQuery);
    }

    [Fact]
    public async Task RetryAsync_uses_last_search_query()
    {
        var service = new FakeSearchService();
        var viewModel = new SearchViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        )
        {
            SearchText = "pierwsza fraza",
        };

        await viewModel.SearchAsync();
        service.RequestedQuery = string.Empty;
        viewModel.SearchText = "inna fraza";

        await viewModel.RetryAsync();

        Assert.Equal("pierwsza fraza", service.RequestedQuery);
    }

    [Fact]
    public async Task SearchAsync_sets_error_message_when_service_fails()
    {
        var service = new FakeSearchService
        {
            ExceptionToThrow = new HttpRequestException("boom"),
        };
        var viewModel = new SearchViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        )
        {
            SearchText = "test",
        };

        await viewModel.SearchAsync();

        Assert.True(viewModel.HasError);
        Assert.Equal("Nie udało się wyszukać treści. Spróbuj ponownie.", viewModel.ErrorMessage);
        Assert.Equal(viewModel.ErrorMessage, viewModel.StatusMessage);
        Assert.Empty(viewModel.Results);
    }

    private sealed class FakeSearchService : IWordPressSearchService
    {
        public IReadOnlyList<SearchResultItem> Items { get; init; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public SearchScope RequestedScope { get; set; }

        public string RequestedQuery { get; set; } = string.Empty;

        public int RequestedPageSize { get; private set; }

        public Task<IReadOnlyList<SearchResultItem>> SearchAsync(
            SearchScope scope,
            string query,
            int pageSize,
            CancellationToken cancellationToken = default
        )
        {
            RequestedScope = scope;
            RequestedQuery = query;
            RequestedPageSize = pageSize;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Items);
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
