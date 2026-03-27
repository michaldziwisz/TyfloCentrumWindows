using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressCatalogService : IWordPressCatalogService
{
    private static readonly TimeSpan CategoriesCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ItemsCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITransientContentCache _cache;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressCatalogService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        ITransientContentCache cache
    )
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<IReadOnlyList<WpCategorySummary>> GetCategoriesAsync(
        ContentSource source,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(GetBaseUri(source), "wp/v2/categories"));
        builder.Query = "per_page=100&orderby=name&order=asc&_fields=id,name,count";

        return await _cache.GetOrCreateAsync(
            $"wp-categories:{builder.Uri.AbsoluteUri}",
            CategoriesCacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var items = await response.Content.ReadFromJsonAsync<List<WpCategorySummary>>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return (IReadOnlyList<WpCategorySummary>)(items ?? []);
            },
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<WpPostSummary>> GetItemsAsync(
        ContentSource source,
        int pageSize,
        int? categoryId = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetItemsPageAsync(
            source,
            pageSize,
            1,
            categoryId,
            cancellationToken
        );
        return result.Items;
    }

    public async Task<PagedResult<WpPostSummary>> GetItemsPageAsync(
        ContentSource source,
        int pageSize,
        int pageNumber,
        int? categoryId = null,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedPageSize = Math.Max(1, pageSize);
        var normalizedPageNumber = Math.Max(1, pageNumber);
        var builder = new UriBuilder(new Uri(GetBaseUri(source), "wp/v2/posts"));

        var queryParts = new List<string>
        {
            "context=embed",
            $"per_page={normalizedPageSize}",
            $"page={normalizedPageNumber}",
            "orderby=date",
            "order=desc",
            "_fields=id,date,link,title,excerpt",
        };

        if (categoryId is int value)
        {
            queryParts.Add($"categories={value}");
        }

        builder.Query = string.Join("&", queryParts);

        return await _cache.GetOrCreateAsync(
            $"wp-catalog-page:{builder.Uri.AbsoluteUri}",
            ItemsCacheTtl,
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
                    HasMorePages(response, normalizedPageNumber, normalizedPageSize, items?.Count ?? 0)
                );
            },
            cancellationToken
        );
    }

    private Uri GetBaseUri(ContentSource source) =>
        source == ContentSource.Podcast ? _options.TyflopodcastApiBaseUrl : _options.TyfloswiatApiBaseUrl;

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
