using System.Net;
using System.Text;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class WordPressCatalogServiceTests
{
    [Fact]
    public async Task GetCategoriesAsync_requests_categories_endpoint_for_selected_source()
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
                      { "id": 7, "name": "Sprzęt", "count": 12 }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressCatalogService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var categories = await service.GetCategoriesAsync(ContentSource.Podcast);

        var category = Assert.Single(categories);
        Assert.Equal("Sprzęt", category.Name);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://podcasts.example/wp-json/wp/v2/categories", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("per_page=100", requestedUri.Query);
        Assert.Contains("_fields=id,name,count", requestedUri.Query);
    }

    [Fact]
    public async Task GetItemsAsync_adds_category_filter_when_category_is_selected()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "X-WP-TotalPages", "3" },
                },
                Content = new StringContent(
                    """
                    [
                      {
                        "id": 21,
                        "date": "2026-03-19T08:00:00",
                        "link": "https://articles.example/posts/21",
                        "title": { "rendered": "Artykuł testowy" },
                        "excerpt": { "rendered": "<p>Opis</p>" }
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressCatalogService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var items = await service.GetItemsAsync(ContentSource.Article, 25, 9);

        var item = Assert.Single(items);
        Assert.Equal("Artykuł testowy", item.Title.Rendered);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://articles.example/wp-json/wp/v2/posts", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("per_page=25", requestedUri.Query);
        Assert.Contains("categories=9", requestedUri.Query);
        Assert.Contains("context=embed", requestedUri.Query);
    }

    [Fact]
    public async Task GetItemsPageAsync_uses_requested_page_and_reports_has_more()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "X-WP-TotalPages", "4" },
                },
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressCatalogService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var result = await service.GetItemsPageAsync(ContentSource.Podcast, 25, 2);

        Assert.NotNull(requestedUri);
        Assert.Contains("page=2", requestedUri!.Query);
        Assert.True(result.HasMoreItems);
    }

    [Fact]
    public async Task GetCategoriesAsync_uses_cache_for_repeated_identical_request()
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
        var service = new WordPressCatalogService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        await service.GetCategoriesAsync(ContentSource.Podcast);
        await service.GetCategoriesAsync(ContentSource.Podcast);

        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task GetItemsPageAsync_uses_cache_for_repeated_identical_request()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "X-WP-TotalPages", "1" },
                },
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressCatalogService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        await service.GetItemsPageAsync(ContentSource.Article, 25, 1, 9);
        await service.GetItemsPageAsync(ContentSource.Article, 25, 1, 9);

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
