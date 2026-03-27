using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressPostDetailsService : IWordPressPostDetailsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITransientContentCache _cache;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressPostDetailsService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        ITransientContentCache cache
    )
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<WpPostDetail> GetPostAsync(
        ContentSource source,
        int postId,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(GetBaseUri(source), $"wp/v2/posts/{postId}"));
        builder.Query = "_fields=id,date,link,title,excerpt,content,guid";

        return await _cache.GetOrCreateAsync(
            $"wp-post-detail:{builder.Uri.AbsoluteUri}",
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

                return item ?? throw new InvalidOperationException("Brak danych szczegółowych wpisu.");
            },
            cancellationToken
        );
    }

    private Uri GetBaseUri(ContentSource source) =>
        source == ContentSource.Podcast ? _options.TyflopodcastApiBaseUrl : _options.TyfloswiatApiBaseUrl;
}
