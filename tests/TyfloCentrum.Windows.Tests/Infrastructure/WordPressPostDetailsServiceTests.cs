using System.Net;
using System.Text;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class WordPressPostDetailsServiceTests
{
    [Fact]
    public async Task GetPostAsync_requests_post_details_with_expected_fields()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": 77,
                      "date": "2026-03-20T12:00:00",
                      "link": "https://podcasts.example/posts/77",
                      "title": { "rendered": "Podcast testowy" },
                      "excerpt": { "rendered": "<p>Opis</p>" },
                      "content": { "rendered": "<p>Treść</p>" },
                      "guid": { "rendered": "https://podcasts.example/?p=77" }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressPostDetailsService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var item = await service.GetPostAsync(ContentSource.Podcast, 77);

        Assert.Equal(77, item.Id);
        Assert.Equal("Podcast testowy", item.Title.Rendered);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://podcasts.example/wp-json/wp/v2/posts/77", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("_fields=id,date,link,title,excerpt,content,guid", requestedUri.Query);
    }

    [Fact]
    public async Task GetPostAsync_uses_cache_for_repeated_identical_request()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": 77,
                      "date": "2026-03-20T12:00:00",
                      "link": "https://podcasts.example/posts/77",
                      "title": { "rendered": "Podcast testowy" },
                      "excerpt": { "rendered": "<p>Opis</p>" },
                      "content": { "rendered": "<p>Treść</p>" },
                      "guid": { "rendered": "https://podcasts.example/?p=77" }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressPostDetailsService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        await service.GetPostAsync(ContentSource.Podcast, 77);
        await service.GetPostAsync(ContentSource.Podcast, 77);

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
