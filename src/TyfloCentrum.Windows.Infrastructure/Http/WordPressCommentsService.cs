using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressCommentsService : IWordPressCommentsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITransientContentCache _cache;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressCommentsService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        ITransientContentCache cache
    )
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<IReadOnlyList<WordPressComment>> GetCommentsAsync(
        int postId,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyflopodcastApiBaseUrl, "wp/v2/comments"));
        builder.Query = $"post={postId}&per_page=100";

        return await _cache.GetOrCreateAsync(
            $"wp-comments:{builder.Uri.AbsoluteUri}",
            CacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var items = await response.Content.ReadFromJsonAsync<List<WordPressComment>>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return (IReadOnlyList<WordPressComment>)(items ?? []);
            },
            cancellationToken
        );
    }
}
