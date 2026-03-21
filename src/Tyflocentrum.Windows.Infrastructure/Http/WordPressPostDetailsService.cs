using System.Net.Http.Json;
using System.Text.Json;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.Infrastructure.Http;

public sealed class WordPressPostDetailsService : IWordPressPostDetailsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TyflocentrumEndpointsOptions _options;

    public WordPressPostDetailsService(HttpClient httpClient, TyflocentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<WpPostDetail> GetPostAsync(
        ContentSource source,
        int postId,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(GetBaseUri(source), $"wp/v2/posts/{postId}"));
        builder.Query = "_fields=id,date,link,title,excerpt,content,guid";

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var item = await response.Content.ReadFromJsonAsync<WpPostDetail>(
            SerializerOptions,
            cancellationToken
        );

        return item ?? throw new InvalidOperationException("Brak danych szczegółowych wpisu.");
    }

    private Uri GetBaseUri(ContentSource source) =>
        source == ContentSource.Podcast ? _options.TyflopodcastApiBaseUrl : _options.TyfloswiatApiBaseUrl;
}
