using System.Net.Http.Json;
using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Options;
using Microsoft.Extensions.Options;

namespace TyfloCentrum.PushService.Services;

public class WordPressFeedClient
{
    private readonly HttpClient _httpClient;
    private readonly PushServiceOptions _options;

    public WordPressFeedClient(HttpClient httpClient, IOptions<PushServiceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public virtual Task<IReadOnlyList<WordPressPostEnvelope>> FetchLatestPodcastsAsync(CancellationToken cancellationToken)
    {
        return FetchLatestAsync(_options.TyflopodcastWordPressBaseUrl, cancellationToken);
    }

    public virtual Task<IReadOnlyList<WordPressPostEnvelope>> FetchLatestArticlesAsync(CancellationToken cancellationToken)
    {
        return FetchLatestAsync(_options.TyfloswiatWordPressBaseUrl, cancellationToken);
    }

    private async Task<IReadOnlyList<WordPressPostEnvelope>> FetchLatestAsync(
        string baseUrl,
        CancellationToken cancellationToken
    )
    {
        var uri = BuildPostsUri(baseUrl, _options.PollPerPage);
        var response = await _httpClient.GetFromJsonAsync<List<WordPressPostEnvelope>>(uri, cancellationToken);
        return response ?? [];
    }

    private static Uri BuildPostsUri(string baseUrl, int perPage)
    {
        var builder = new UriBuilder(new Uri(new Uri(baseUrl), "wp/v2/posts"));
        builder.Query = $"context=embed&per_page={perPage}&_fields=id,date,link,title";
        return builder.Uri;
    }
}
