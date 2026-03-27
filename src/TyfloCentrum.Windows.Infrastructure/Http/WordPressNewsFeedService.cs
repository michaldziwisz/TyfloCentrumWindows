using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressNewsFeedService : INewsFeedService
{
    private const int MaxWordPressPageSize = 100;
    private static readonly TimeSpan FeedPageCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITransientContentCache _cache;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressNewsFeedService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        ITransientContentCache cache
    )
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<IReadOnlyList<NewsFeedItem>> GetLatestItemsAsync(
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetLatestItemsPageAsync(pageSize, 1, cancellationToken);
        return result.Items;
    }

    public async Task<PagedResult<NewsFeedItem>> GetLatestItemsPageAsync(
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedPageSize = Math.Max(1, pageSize);
        var normalizedPageNumber = Math.Max(1, pageNumber);
        var itemsNeededForRequestedPage = normalizedPageSize * normalizedPageNumber;

        var podcastTask = FetchTopPostSummariesAsync(
            _options.TyflopodcastApiBaseUrl,
            itemsNeededForRequestedPage,
            cancellationToken
        );

        var articleTask = FetchTopPostSummariesAsync(
            _options.TyfloswiatApiBaseUrl,
            itemsNeededForRequestedPage,
            cancellationToken
        );

        await Task.WhenAll(podcastTask, articleTask);

        var podcastFeed = podcastTask.Result;
        var articleFeed = articleTask.Result;

        var merged = podcastFeed.Items
            .Select(item => new NewsFeedItem(NewsItemKind.Podcast, item))
            .Concat(articleFeed.Items.Select(item => new NewsFeedItem(NewsItemKind.Article, item)))
            .OrderByDescending(item => item.Post.PublishedAtUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.Post.Date, StringComparer.Ordinal)
            .ThenBy(item => item.Kind == NewsItemKind.Podcast ? 0 : 1)
            .ThenByDescending(item => item.Post.Id)
            .ToArray();

        var skip = (normalizedPageNumber - 1) * normalizedPageSize;
        var pageItems = merged.Skip(skip).Take(normalizedPageSize).ToArray();
        var hasMoreItems =
            merged.Length > skip + normalizedPageSize
            || podcastFeed.HasMoreItems
            || articleFeed.HasMoreItems;

        return new PagedResult<NewsFeedItem>(pageItems, hasMoreItems);
    }

    private async Task<AggregatedPostSummaryResult> FetchTopPostSummariesAsync(
        Uri baseUri,
        int totalItemsNeeded,
        CancellationToken cancellationToken
    )
    {
        var items = new List<WpPostSummary>(Math.Max(0, totalItemsNeeded));
        var pageNumber = 1;
        var hasMoreItems = false;
        var pageSize = Math.Min(MaxWordPressPageSize, Math.Max(1, totalItemsNeeded));

        while (items.Count < totalItemsNeeded)
        {
            var page = await FetchPostSummariesPageAsync(
                baseUri,
                pageSize,
                pageNumber,
                cancellationToken
            );

            items.AddRange(page.Items);
            hasMoreItems = page.HasMoreItems;

            if (!page.HasMoreItems || page.Items.Count == 0)
            {
                break;
            }

            pageNumber++;
        }

        return new AggregatedPostSummaryResult(items, hasMoreItems);
    }

    private async Task<PagedResult<WpPostSummary>> FetchPostSummariesPageAsync(
        Uri baseUri,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken
    )
    {
        var builder = new UriBuilder(new Uri(baseUri, "wp/v2/posts"));
        builder.Query =
            $"context=embed&per_page={pageSize}&page={pageNumber}&orderby=date&order=desc&_fields=id,date,link,title,excerpt";

        return await _cache.GetOrCreateAsync(
            $"wp-news-page:{builder.Uri.AbsoluteUri}",
            FeedPageCacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var items = await response.Content.ReadFromJsonAsync<List<WpPostSummary>>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return new PagedResult<WpPostSummary>(
                    items ?? [],
                    HasMorePages(response, pageNumber, pageSize, items?.Count ?? 0)
                );
            },
            cancellationToken
        );
    }

    private static bool HasMorePages(
        HttpResponseMessage response,
        int currentPageNumber,
        int pageSize,
        int currentCount
    )
    {
        if (
            response.Headers.TryGetValues("X-WP-TotalPages", out var values)
            && int.TryParse(values.FirstOrDefault(), out var totalPages)
        )
        {
            return currentPageNumber < totalPages;
        }

        return currentCount >= pageSize;
    }

    private sealed record AggregatedPostSummaryResult(
        IReadOnlyList<WpPostSummary> Items,
        bool HasMoreItems
    );
}
