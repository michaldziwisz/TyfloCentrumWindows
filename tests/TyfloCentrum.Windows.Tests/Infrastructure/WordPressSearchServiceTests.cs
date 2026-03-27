using System.Net;
using System.Text;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class WordPressSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_trims_query_and_queries_both_sources_for_all_scope()
    {
        var requests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!);

            var json = request.RequestUri!.Host switch
            {
                "podcasts.example" =>
                    """
                    [
                      {
                        "id": 11,
                        "date": "2026-03-19T09:00:00",
                        "link": "https://podcasts.example/posts/11",
                        "title": { "rendered": "Ala ma kota i mikrofon" },
                        "excerpt": { "rendered": "<p>Podcast</p>" }
                      }
                    ]
                    """,
                "articles.example" =>
                    """
                    [
                      {
                        "id": 21,
                        "date": "2026-03-18T09:00:00",
                        "link": "https://articles.example/posts/21",
                        "title": { "rendered": "Ala i dostępność" },
                        "excerpt": { "rendered": "<p>Artykuł</p>" }
                      }
                    ]
                    """,
                _ => "[]",
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressSearchService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var items = await service.SearchAsync(SearchScope.All, "  Ala ma kota  ", 100);

        Assert.Equal(2, requests.Count);
        Assert.Collection(
            requests.OrderBy(item => item.Host),
            request =>
            {
                Assert.Equal("articles.example", request.Host);
                Assert.Contains("search=Ala%20ma%20kota", request.Query);
                Assert.Contains("context=embed", request.Query);
                Assert.Contains("_fields=id,date,link,title,excerpt", request.Query);
            },
            request =>
            {
                Assert.Equal("podcasts.example", request.Host);
                Assert.Contains("search=Ala%20ma%20kota", request.Query);
                Assert.Contains("per_page=100", request.Query);
            }
        );

        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal(ContentSource.Podcast, item.Source);
                Assert.Equal("Ala ma kota i mikrofon", item.Post.Title.Rendered);
            },
            item =>
            {
                Assert.Equal(ContentSource.Article, item.Source);
                Assert.Equal("Ala i dostępność", item.Post.Title.Rendered);
            }
        );
    }

    [Fact]
    public async Task SearchAsync_sorts_results_by_relevance_then_date_and_source()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var json = request.RequestUri!.Host switch
            {
                "podcasts.example" =>
                    """
                    [
                      {
                        "id": 12,
                        "date": "2026-03-18T10:00:00",
                        "link": "https://podcasts.example/posts/12",
                        "title": { "rendered": "Zażółć gęślą jaźń test" },
                        "excerpt": { "rendered": "<p>Dokładne dopasowanie</p>" }
                      },
                      {
                        "id": 11,
                        "date": "2026-03-17T10:00:00",
                        "link": "https://podcasts.example/posts/11",
                        "title": { "rendered": "Test o gęślą i jaźni, zażółć" },
                        "excerpt": { "rendered": "<p>Dopasowanie tokenów</p>" }
                      }
                    ]
                    """,
                "articles.example" =>
                    """
                    [
                      {
                        "id": 22,
                        "date": "2026-03-18T10:00:00",
                        "link": "https://articles.example/posts/22",
                        "title": { "rendered": "Zażółć gęślą jaźń test" },
                        "excerpt": { "rendered": "<p>Równoległy artykuł</p>" }
                      },
                      {
                        "id": 21,
                        "date": "2026-03-19T10:00:00",
                        "link": "https://articles.example/posts/21",
                        "title": { "rendered": "Inny artykuł" },
                        "excerpt": { "rendered": "<p>Brak dopasowania</p>" }
                      }
                    ]
                    """,
                _ => "[]",
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressSearchService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var items = await service.SearchAsync(SearchScope.All, "zazolc gesla jazn test", 100);

        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal(ContentSource.Podcast, item.Source);
                Assert.Equal(12, item.Post.Id);
            },
            item =>
            {
                Assert.Equal(ContentSource.Article, item.Source);
                Assert.Equal(22, item.Post.Id);
            },
            item =>
            {
                Assert.Equal(ContentSource.Podcast, item.Source);
                Assert.Equal(11, item.Post.Id);
            },
            item =>
            {
                Assert.Equal(ContentSource.Article, item.Source);
                Assert.Equal(21, item.Post.Id);
            }
        );
    }

    [Fact]
    public async Task SearchAsync_uses_cache_for_repeated_identical_request()
    {
        var requests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressSearchService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        await service.SearchAsync(SearchScope.All, "cache test", 50);
        await service.SearchAsync(SearchScope.All, "cache test", 50);

        Assert.Equal(2, requests.Count);
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
