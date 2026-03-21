using System.Net.Http.Json;
using System.Text.Json;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.Infrastructure.Http;

public sealed class WordPressNewsFeedService : INewsFeedService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TyflocentrumEndpointsOptions _options;

    public WordPressNewsFeedService(HttpClient httpClient, TyflocentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
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

        var podcastTask = FetchPostSummariesPageAsync(
            _options.TyflopodcastApiBaseUrl,
            normalizedPageSize,
            normalizedPageNumber,
            cancellationToken
        );

        var articleTask = FetchPostSummariesPageAsync(
            _options.TyfloswiatApiBaseUrl,
            normalizedPageSize,
            normalizedPageNumber,
            cancellationToken
        );

        await Task.WhenAll(podcastTask, articleTask);

        var podcastPage = podcastTask.Result;
        var articlePage = articleTask.Result;

        var merged = podcastPage.Items
            .Select(item => new NewsFeedItem(NewsItemKind.Podcast, item))
            .Concat(articlePage.Items.Select(item => new NewsFeedItem(NewsItemKind.Article, item)))
            .OrderByDescending(item => item.Post.Date, StringComparer.Ordinal)
            .ThenBy(item => item.Kind == NewsItemKind.Podcast ? 0 : 1)
            .ThenByDescending(item => item.Post.Id)
            .ToArray();

        return new PagedResult<NewsFeedItem>(merged, podcastPage.HasMoreItems || articlePage.HasMoreItems);
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

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<WpPostSummary>>(
            SerializerOptions,
            cancellationToken
        );

        return new PagedResult<WpPostSummary>(
            items ?? [],
            HasMorePages(response, pageNumber, pageSize, items?.Count ?? 0)
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
}
