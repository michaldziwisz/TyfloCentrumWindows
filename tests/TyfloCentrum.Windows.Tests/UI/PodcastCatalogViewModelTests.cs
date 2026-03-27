using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class PodcastCatalogViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_loads_categories_with_all_bucket_and_first_page_of_items()
    {
        var service = new FakeCatalogService
        {
            Categories =
            [
                new WpCategorySummary { Id = 10, Name = "Nowości sprzętowe", Count = 4 },
                new WpCategorySummary { Id = 20, Name = "Aplikacje", Count = 7 },
            ],
            Items =
            [
                new WpPostSummary
                {
                    Id = 101,
                    Date = "2026-03-18T08:30:00",
                    Link = "https://example.invalid/post/101",
                    Title = new RenderedText("Podcast testowy"),
                    Excerpt = new RenderedText("<p>Opis testowy</p>"),
                },
            ],
        };

        var viewModel = new PodcastCatalogViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        );

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(3, viewModel.Categories.Count);
        Assert.Equal("Wszystkie kategorie", viewModel.Categories[0].Name);
        Assert.Same(viewModel.Categories[0], viewModel.SelectedCategory);
        Assert.Equal("Wszystkie podcasty", viewModel.CurrentItemsHeading);
        Assert.Single(viewModel.Items);
        Assert.Equal(ContentSource.Podcast, service.RequestedSource);
        Assert.Equal(20, service.RequestedPageSize);
        Assert.Null(service.RequestedCategoryId);
    }

    [Fact]
    public async Task SelectCategoryAsync_loads_items_for_selected_category()
    {
        var service = new FakeCatalogService
        {
            Categories =
            [
                new WpCategorySummary { Id = 10, Name = "Nowości sprzętowe", Count = 4 },
            ],
            Items =
            [
                new WpPostSummary
                {
                    Id = 102,
                    Date = "2026-03-18T08:30:00",
                    Link = "https://example.invalid/post/102",
                    Title = new RenderedText("Podcast w kategorii"),
                    Excerpt = new RenderedText("<p>Opis testowy</p>"),
                },
            ],
        };

        var viewModel = new PodcastCatalogViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        );
        await viewModel.LoadIfNeededAsync();

        await viewModel.SelectCategoryAsync(viewModel.Categories[1]);

        Assert.Equal(10, service.RequestedCategoryId);
        Assert.Equal("Podcasty w kategorii: Nowości sprzętowe", viewModel.CurrentItemsHeading);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task LoadMoreAsync_appends_next_page_of_items()
    {
        var service = new FakeCatalogService
        {
            Categories =
            [
                new WpCategorySummary { Id = 10, Name = "Nowości sprzętowe", Count = 4 },
            ],
            Pages =
            {
                [1] =
                [
                    new WpPostSummary
                    {
                        Id = 201,
                        Date = "2026-03-18T08:30:00",
                        Link = "https://example.invalid/post/201",
                        Title = new RenderedText("Podcast pierwszy"),
                        Excerpt = new RenderedText("<p>Opis 1</p>"),
                    },
                ],
                [2] =
                [
                    new WpPostSummary
                    {
                        Id = 202,
                        Date = "2026-03-17T08:30:00",
                        Link = "https://example.invalid/post/202",
                        Title = new RenderedText("Podcast drugi"),
                        Excerpt = new RenderedText("<p>Opis 2</p>"),
                    },
                ],
            },
            HasMoreByPage =
            {
                [1] = true,
                [2] = false,
            },
        };

        var viewModel = new PodcastCatalogViewModel(
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
    public async Task SelectCategoryAsync_applies_latest_selection_after_quick_successive_changes()
    {
        var firstCategoryGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var service = new FakeCatalogService
        {
            Categories =
            [
                new WpCategorySummary { Id = 10, Name = "Nowości sprzętowe", Count = 4 },
                new WpCategorySummary { Id = 20, Name = "Aplikacje", Count = 7 },
            ],
            DeferredCategoryId = 10,
            DeferredItemsGate = firstCategoryGate,
        };

        service.CategoryItems[10] =
        [
            new WpPostSummary
            {
                Id = 301,
                Date = "2026-03-18T08:30:00",
                Link = "https://example.invalid/post/301",
                Title = new RenderedText("Podcast kategorii 10"),
                Excerpt = new RenderedText("<p>Opis 10</p>"),
            },
        ];

        service.CategoryItems[20] =
        [
            new WpPostSummary
            {
                Id = 302,
                Date = "2026-03-18T08:31:00",
                Link = "https://example.invalid/post/302",
                Title = new RenderedText("Podcast kategorii 20"),
                Excerpt = new RenderedText("<p>Opis 20</p>"),
            },
        ];

        var viewModel = new PodcastCatalogViewModel(
            service,
            new FakeExternalLinkLauncher(),
            new ContentTypeAnnouncementPreferenceService()
        );
        await viewModel.LoadIfNeededAsync();

        var firstSelectionTask = viewModel.SelectCategoryAsync(viewModel.Categories[1]);
        await Task.Yield();
        await viewModel.SelectCategoryAsync(viewModel.Categories[2]);

        firstCategoryGate.SetResult(true);
        await firstSelectionTask;

        Assert.Equal(20, viewModel.SelectedCategory?.Id);
        Assert.Equal("Podcasty w kategorii: Aplikacje", viewModel.CurrentItemsHeading);
        Assert.Single(viewModel.Items);
        Assert.Equal("Podcast kategorii 20", viewModel.Items[0].Title);
        Assert.Equal([null, 10, 20], service.RequestedCategoryIds);
    }

    private sealed class FakeCatalogService : IWordPressCatalogService
    {
        public IReadOnlyList<WpCategorySummary> Categories { get; init; } = [];

        public IReadOnlyList<WpPostSummary> Items { get; init; } = [];

        public Dictionary<int, IReadOnlyList<WpPostSummary>> CategoryItems { get; } = [];

        public Dictionary<int, IReadOnlyList<WpPostSummary>> Pages { get; } = [];

        public Dictionary<int, bool> HasMoreByPage { get; } = [];

        public int? DeferredCategoryId { get; init; }

        public TaskCompletionSource<bool>? DeferredItemsGate { get; init; }

        public ContentSource RequestedSource { get; private set; }

        public int RequestedPageSize { get; private set; }

        public int? RequestedCategoryId { get; private set; }

        public List<int> RequestedPages { get; } = [];

        public List<int?> RequestedCategoryIds { get; } = [];

        public Task<IReadOnlyList<WpCategorySummary>> GetCategoriesAsync(
            ContentSource source,
            CancellationToken cancellationToken = default
        )
        {
            RequestedSource = source;
            return Task.FromResult(Categories);
        }

        public Task<IReadOnlyList<WpPostSummary>> GetItemsAsync(
            ContentSource source,
            int pageSize,
            int? categoryId = null,
            CancellationToken cancellationToken = default
        )
        {
            RequestedSource = source;
            RequestedPageSize = pageSize;
            RequestedCategoryId = categoryId;
            return Task.FromResult(Items);
        }

        public Task<PagedResult<WpPostSummary>> GetItemsPageAsync(
            ContentSource source,
            int pageSize,
            int pageNumber,
            int? categoryId = null,
            CancellationToken cancellationToken = default
        )
        {
            return GetItemsPageAsyncCore(source, pageSize, pageNumber, categoryId, cancellationToken);
        }

        private async Task<PagedResult<WpPostSummary>> GetItemsPageAsyncCore(
            ContentSource source,
            int pageSize,
            int pageNumber,
            int? categoryId,
            CancellationToken cancellationToken
        )
        {
            RequestedSource = source;
            RequestedPageSize = pageSize;
            RequestedCategoryId = categoryId;
            RequestedPages.Add(pageNumber);
            RequestedCategoryIds.Add(categoryId);

            if (
                pageNumber == 1
                && DeferredItemsGate is not null
                && DeferredCategoryId == categoryId
                && !DeferredItemsGate.Task.IsCompleted
            )
            {
                await DeferredItemsGate.Task.WaitAsync(cancellationToken);
            }

            var items =
                Pages.TryGetValue(pageNumber, out var pageItems) ? pageItems
                : CategoryItems.TryGetValue(categoryId ?? -1, out var categoryItems) ? categoryItems
                : Items;
            var hasMore = HasMoreByPage.TryGetValue(pageNumber, out var value) && value;
            return new PagedResult<WpPostSummary>(items, hasMore);
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
