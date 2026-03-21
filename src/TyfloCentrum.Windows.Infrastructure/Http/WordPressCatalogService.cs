using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressCatalogService : IWordPressCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressCatalogService(HttpClient httpClient, TyfloCentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyList<WpCategorySummary>> GetCategoriesAsync(
        ContentSource source,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(GetBaseUri(source), "wp/v2/categories"));
        builder.Query = "per_page=100&orderby=name&order=asc&_fields=id,name,count";

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<WpCategorySummary>>(
            SerializerOptions,
            cancellationToken
        );

        return items ?? [];
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
            HasMorePages(response, normalizedPageNumber, normalizedPageSize, items?.Count ?? 0)
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
