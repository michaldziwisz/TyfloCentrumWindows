using System.Net;
using System.Text;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class WordPressPageDetailsServiceTests
{
    [Fact]
    public async Task GetPageAsync_requests_page_details_with_expected_fields()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return JsonResponse(
                """
                {
                  "id": 11085,
                  "date": "2026-03-20T12:00:00",
                  "link": "https://podcasts.example/tekstowe-wersje-audycji/test/",
                  "title": { "rendered": "Wersja tekstowa" },
                  "excerpt": { "rendered": "<p>Opis</p>" },
                  "content": { "rendered": "<p>Treść</p>" },
                  "guid": { "rendered": "https://podcasts.example/?page_id=11085" }
                }
                """
            );
        });

        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var item = await service.GetPageAsync(ContentSource.Podcast, 11085);

        Assert.Equal(11085, item.Id);
        Assert.Equal("Wersja tekstowa", item.Title.Rendered);
        Assert.NotNull(requestedUri);
        Assert.Equal(
            "https://podcasts.example/wp-json/wp/v2/pages/11085",
            requestedUri!.GetLeftPart(UriPartial.Path)
        );
        Assert.Contains("_fields=id,date,link,title,excerpt,content,guid", requestedUri.Query);
    }

    [Fact]
    public async Task GetPageBySlugAsync_requests_page_by_slug()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return JsonResponse(
                """
                [
                  {
                    "id": 123,
                    "date": "2026-03-20T12:00:00",
                    "link": "https://podcasts.example/tekstowe-wersje-audycji/test/",
                    "title": { "rendered": "Wersja tekstowa" },
                    "excerpt": { "rendered": "<p>Opis</p>" },
                    "content": { "rendered": "<p>Treść</p>" },
                    "guid": { "rendered": "https://podcasts.example/?page_id=123" }
                  }
                ]
                """
            );
        });

        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var item = await service.GetPageBySlugAsync(ContentSource.Podcast, "test-wersja-tekstowa");

        Assert.NotNull(item);
        Assert.Equal(123, item!.Id);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://podcasts.example/wp-json/wp/v2/pages", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("slug=test-wersja-tekstowa", requestedUri.Query);
        Assert.Contains("per_page=1", requestedUri.Query);
    }

    private static WordPressPageDetailsService CreateService(HttpClient httpClient)
    {
        return new WordPressPageDetailsService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
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
