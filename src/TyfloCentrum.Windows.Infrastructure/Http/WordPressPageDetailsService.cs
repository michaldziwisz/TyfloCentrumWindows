using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressPageDetailsService : IWordPressPageDetailsService
{
    private const string DetailFields = "id,date,link,title,excerpt,content,guid";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITransientContentCache _cache;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressPageDetailsService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        ITransientContentCache cache
    )
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<WpPostDetail> GetPageAsync(
        ContentSource source,
        int pageId,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(GetBaseUri(source), $"wp/v2/pages/{pageId}"));
        builder.Query = $"_fields={DetailFields}";

        return await _cache.GetOrCreateAsync(
            $"wp-page-detail:{builder.Uri.AbsoluteUri}",
            CacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var item = await response.Content.ReadFromJsonAsync<WpPostDetail>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return item ?? throw new InvalidOperationException("Brak danych szczegółowych strony.");
            },
            cancellationToken
        );
    }

    public async Task<WpPostDetail?> GetPageBySlugAsync(
        ContentSource source,
        string slug,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var builder = new UriBuilder(new Uri(GetBaseUri(source), "wp/v2/pages"));
        builder.Query = $"slug={Uri.EscapeDataString(slug.Trim())}&per_page=1&_fields={DetailFields}";

        return await _cache.GetOrCreateAsync(
            $"wp-page-by-slug:{builder.Uri.AbsoluteUri}",
            CacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var items = await response.Content.ReadFromJsonAsync<WpPostDetail[]>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return items?.FirstOrDefault();
            },
            cancellationToken
        );
    }

    private Uri GetBaseUri(ContentSource source) =>
        source == ContentSource.Podcast ? _options.TyflopodcastApiBaseUrl : _options.TyfloswiatApiBaseUrl;
}
