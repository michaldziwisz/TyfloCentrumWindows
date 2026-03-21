using System.Net.Http.Json;
using System.Text.Json;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.Infrastructure.Http;

public sealed class WordPressCommentsService : IWordPressCommentsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TyflocentrumEndpointsOptions _options;

    public WordPressCommentsService(HttpClient httpClient, TyflocentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyList<WordPressComment>> GetCommentsAsync(
        int postId,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyflopodcastApiBaseUrl, "wp/v2/comments"));
        builder.Query = $"post={postId}&per_page=100";

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<WordPressComment>>(
            SerializerOptions,
            cancellationToken
        );

        return items ?? [];
    }
}
