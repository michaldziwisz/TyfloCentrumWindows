using System.Net.Http.Json;
using System.Text.Json;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.Domain.Text;

namespace Tyflocentrum.Windows.Infrastructure.Http;

public sealed class WordPressSearchService : IWordPressSearchService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TyflocentrumEndpointsOptions _options;

    public WordPressSearchService(HttpClient httpClient, TyflocentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        SearchScope scope,
        string query,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return [];
        }

        var normalizedPageSize = Math.Max(1, pageSize);
        var items = scope switch
        {
            SearchScope.Podcasts => (await FetchItemsAsync(
                _options.TyflopodcastApiBaseUrl,
                trimmedQuery,
                normalizedPageSize,
                cancellationToken
            )).Select(item => new SearchResultItem(ContentSource.Podcast, item)),
            SearchScope.Articles => (await FetchItemsAsync(
                _options.TyfloswiatApiBaseUrl,
                trimmedQuery,
                normalizedPageSize,
                cancellationToken
            )).Select(item => new SearchResultItem(ContentSource.Article, item)),
            _ => await FetchAllItemsAsync(trimmedQuery, normalizedPageSize, cancellationToken),
        };

        return SortByRelevance(items, trimmedQuery).ToArray();
    }

    private async Task<IEnumerable<SearchResultItem>> FetchAllItemsAsync(
        string query,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var podcastsTask = FetchItemsAsync(
            _options.TyflopodcastApiBaseUrl,
            query,
            pageSize,
            cancellationToken
        );
        var articlesTask = FetchItemsAsync(
            _options.TyfloswiatApiBaseUrl,
            query,
            pageSize,
            cancellationToken
        );

        await Task.WhenAll(podcastsTask, articlesTask);

        return podcastsTask.Result.Select(item => new SearchResultItem(ContentSource.Podcast, item))
            .Concat(articlesTask.Result.Select(item => new SearchResultItem(ContentSource.Article, item)));
    }

    private async Task<IReadOnlyList<WpPostSummary>> FetchItemsAsync(
        Uri baseUri,
        string query,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var builder = new UriBuilder(new Uri(baseUri, "wp/v2/posts"));
        builder.Query =
            $"context=embed&per_page={pageSize}&search={Uri.EscapeDataString(query)}&orderby=date&order=desc&_fields=id,date,link,title,excerpt";

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

        return items ?? [];
    }

    private static IEnumerable<SearchResultItem> SortByRelevance(
        IEnumerable<SearchResultItem> items,
        string query
    )
    {
        var normalizedQuery = WordPressContentText.NormalizeForSearch(query);
        var normalizedTokens = normalizedQuery
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 1)
            .ToArray();

        return items
            .Select(item => new
            {
                Item = item,
                Score = Score(item, normalizedQuery, normalizedTokens),
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Item.Post.Date, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Item.Source == ContentSource.Podcast ? 0 : 1)
            .ThenByDescending(candidate => candidate.Item.Post.Id)
            .Select(candidate => candidate.Item);
    }

    private static int Score(SearchResultItem item, string normalizedQuery, string[] normalizedTokens)
    {
        var normalizedTitle = WordPressContentText.NormalizeForSearch(item.Post.Title.Rendered);

        if (!string.IsNullOrWhiteSpace(normalizedQuery) && normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 2;
        }

        if (
            normalizedTokens.Length > 0
            && normalizedTokens.All(token => normalizedTitle.Contains(token, StringComparison.Ordinal))
        )
        {
            return 1;
        }

        return 0;
    }
}
