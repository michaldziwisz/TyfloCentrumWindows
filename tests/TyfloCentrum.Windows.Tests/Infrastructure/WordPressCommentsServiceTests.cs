using System.Net;
using System.Text;
using TyfloCentrum.Windows.Infrastructure.Http;
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
            }
        );

        var items = await service.GetCommentsAsync(77);

        var item = Assert.Single(items);
        Assert.Equal("TyfloPodcast", item.AuthorName);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://podcasts.example/wp-json/wp/v2/comments", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("post=77", requestedUri.Query);
        Assert.Contains("per_page=100", requestedUri.Query);
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
