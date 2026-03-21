using System.Net;
using System.Text;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Infrastructure.Http;
using Xunit;

namespace Tyflocentrum.Windows.Tests.Infrastructure;

public sealed class WordPressNewsFeedServiceTests
{
    [Fact]
    public async Task GetLatestItemsAsync_merges_and_sorts_items_from_both_feeds()
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
                        "date": "2026-03-18T09:00:00",
                        "link": "https://podcasts.example/posts/11",
                        "title": { "rendered": "Podcast nowszy" },
                        "excerpt": { "rendered": "<p>Opis podcastu</p>" }
                      },
                      {
                        "id": 10,
                        "date": "2026-03-16T08:00:00",
                        "link": "https://podcasts.example/posts/10",
                        "title": { "rendered": "Podcast starszy" },
                        "excerpt": { "rendered": "<p>Starszy opis</p>" }
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
                        "title": { "rendered": "Artykul rownolegly" },
                        "excerpt": { "rendered": "<p>Opis artykulu</p>" }
                      },
                      {
                        "id": 20,
                        "date": "2026-03-15T07:30:00",
                        "link": "https://articles.example/posts/20",
                        "title": { "rendered": "Artykul starszy" },
                        "excerpt": { "rendered": "<p>Starszy artykul</p>" }
                      }
                    ]
                    """,
                _ => "[]",
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "X-WP-TotalPages", "2" },
                },
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressNewsFeedService(
            httpClient,
            new TyflocentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            }
        );

        var items = await service.GetLatestItemsAsync(5);

        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal(NewsItemKind.Podcast, item.Kind);
                Assert.Equal("Podcast nowszy", item.Post.Title.Rendered);
            },
            item =>
            {
                Assert.Equal(NewsItemKind.Article, item.Kind);
                Assert.Equal("Artykul rownolegly", item.Post.Title.Rendered);
            },
            item =>
            {
                Assert.Equal(NewsItemKind.Podcast, item.Kind);
                Assert.Equal("Podcast starszy", item.Post.Title.Rendered);
            },
            item =>
            {
                Assert.Equal(NewsItemKind.Article, item.Kind);
                Assert.Equal("Artykul starszy", item.Post.Title.Rendered);
            }
        );

        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Equal("/wp-json/wp/v2/posts", request.AbsolutePath);
            Assert.Contains("context=embed", request.Query);
            Assert.Contains("per_page=5", request.Query);
        });
    }

    [Fact]
    public async Task GetLatestItemsPageAsync_uses_requested_page_and_reports_has_more()
    {
        var requests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "X-WP-TotalPages", "3" },
                },
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressNewsFeedService(
            httpClient,
            new TyflocentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            }
        );

        var result = await service.GetLatestItemsPageAsync(5, 2);

        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Contains("page=2", request.Query));
        Assert.True(result.HasMoreItems);
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
