using System.Net;
using System.Text;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class WordPressCommentsServiceTests
{
    [Fact]
    public async Task GetCommentsAsync_requests_comments_endpoint_for_post()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      {
                        "id": 1001,
                        "post": 77,
                        "parent": 0,
                        "author_name": "TyfloPodcast",
                        "date_gmt": "2026-03-27T10:00:00",
                        "content": { "rendered": "<p>Komentarz testowy</p>" }
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressCommentsService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var items = await service.GetCommentsAsync(77);

        var item = Assert.Single(items);
        Assert.Equal("TyfloPodcast", item.AuthorName);
        Assert.Equal(new DateTimeOffset(2026, 3, 27, 10, 0, 0, TimeSpan.Zero), item.PublishedAtUtc);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://podcasts.example/wp-json/wp/v2/comments", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("post=77", requestedUri.Query);
        Assert.Contains("per_page=100", requestedUri.Query);
    }

    [Fact]
    public async Task GetCommentsAsync_uses_cache_for_repeated_identical_request()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressCommentsService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        await service.GetCommentsAsync(77);
        await service.GetCommentsAsync(77);

        Assert.Equal(1, requestCount);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(handler(request));
        }
    }
}
