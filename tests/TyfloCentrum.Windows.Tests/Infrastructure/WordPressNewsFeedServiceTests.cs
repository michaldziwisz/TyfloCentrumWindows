using System.Net;
using System.Text;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

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
                    { "X-WP-TotalPages", "1" },
                },
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressNewsFeedService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
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
            return CreatePagedPostsResponse(
                request,
                request.RequestUri!.Host == "podcasts.example"
                    ? [
                        CreatePost(109, "2026-03-29T09:00:00", "Podcast 9", "https://podcasts.example/posts/109"),
                        CreatePost(108, "2026-03-28T09:00:00", "Podcast 8", "https://podcasts.example/posts/108"),
                        CreatePost(107, "2026-03-27T09:00:00", "Podcast 7", "https://podcasts.example/posts/107"),
                        CreatePost(106, "2026-03-26T09:00:00", "Podcast 6", "https://podcasts.example/posts/106"),
                        CreatePost(105, "2026-03-25T09:00:00", "Podcast 5", "https://podcasts.example/posts/105"),
                        CreatePost(104, "2026-03-24T09:00:00", "Podcast 4", "https://podcasts.example/posts/104"),
                        CreatePost(103, "2026-03-23T09:00:00", "Podcast 3", "https://podcasts.example/posts/103"),
                        CreatePost(102, "2026-03-22T09:00:00", "Podcast 2", "https://podcasts.example/posts/102"),
                        CreatePost(101, "2026-03-21T09:00:00", "Podcast 1", "https://podcasts.example/posts/101"),
                    ]
                    : [
                        CreatePost(209, "2026-03-19T09:00:00", "Artykuł 9", "https://articles.example/posts/209"),
                        CreatePost(208, "2026-03-18T09:00:00", "Artykuł 8", "https://articles.example/posts/208"),
                        CreatePost(207, "2026-03-17T09:00:00", "Artykuł 7", "https://articles.example/posts/207"),
                        CreatePost(206, "2026-03-16T09:00:00", "Artykuł 6", "https://articles.example/posts/206"),
                        CreatePost(205, "2026-03-15T09:00:00", "Artykuł 5", "https://articles.example/posts/205"),
                        CreatePost(204, "2026-03-14T09:00:00", "Artykuł 4", "https://articles.example/posts/204"),
                        CreatePost(203, "2026-03-13T09:00:00", "Artykuł 3", "https://articles.example/posts/203"),
                        CreatePost(202, "2026-03-12T09:00:00", "Artykuł 2", "https://articles.example/posts/202"),
                        CreatePost(201, "2026-03-11T09:00:00", "Artykuł 1", "https://articles.example/posts/201"),
                    ]
            );
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressNewsFeedService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var result = await service.GetLatestItemsPageAsync(5, 2);

        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Contains("page=1", request.Query);
            Assert.Contains("per_page=10", request.Query);
        });
        Assert.Equal(
            ["Podcast 4", "Podcast 3", "Podcast 2", "Podcast 1", "Artykuł 9"],
            result.Items.Select(item => item.Post.Title.Rendered)
        );
        Assert.True(result.HasMoreItems);
    }

    [Fact]
    public async Task GetLatestItemsPageAsync_returns_globally_chronological_pages_across_sources()
    {
        var requests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return CreatePagedPostsResponse(
                request,
                request.RequestUri!.Host == "podcasts.example"
                    ? [
                        CreatePost(104, "2026-03-20T12:00:00", "Podcast 4", "https://podcasts.example/posts/104"),
                        CreatePost(103, "2026-03-19T12:00:00", "Podcast 3", "https://podcasts.example/posts/103"),
                        CreatePost(102, "2026-03-18T12:00:00", "Podcast 2", "https://podcasts.example/posts/102"),
                        CreatePost(101, "2026-03-17T12:00:00", "Podcast 1", "https://podcasts.example/posts/101"),
                    ]
                    : [
                        CreatePost(202, "2026-03-16T12:00:00", "Artykuł 2", "https://articles.example/posts/202"),
                        CreatePost(201, "2026-03-15T12:00:00", "Artykuł 1", "https://articles.example/posts/201"),
                    ]
            );
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressNewsFeedService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var page1 = await service.GetLatestItemsPageAsync(2, 1);
        var page2 = await service.GetLatestItemsPageAsync(2, 2);
        var page3 = await service.GetLatestItemsPageAsync(2, 3);

        Assert.Equal(["Podcast 4", "Podcast 3"], page1.Items.Select(item => item.Post.Title.Rendered));
        Assert.Equal(["Podcast 2", "Podcast 1"], page2.Items.Select(item => item.Post.Title.Rendered));
        Assert.Equal(["Artykuł 2", "Artykuł 1"], page3.Items.Select(item => item.Post.Title.Rendered));
        Assert.False(page3.HasMoreItems);
    }

    [Fact]
    public async Task GetLatestItemsPageAsync_returns_only_requested_global_page_size()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
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
                        "date": "2026-03-17T09:00:00",
                        "link": "https://articles.example/posts/21",
                        "title": { "rendered": "Artykul posrodku" },
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
                    { "X-WP-TotalPages", "1" },
                },
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new WordPressNewsFeedService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        var page = await service.GetLatestItemsPageAsync(2, 1);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(["Podcast nowszy", "Artykul posrodku"], page.Items.Select(item => item.Post.Title.Rendered));
        Assert.True(page.HasMoreItems);
    }

    [Fact]
    public async Task GetLatestItemsAsync_uses_cache_for_repeated_identical_request()
    {
        var requests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!);
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
        var service = new WordPressNewsFeedService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
                TyfloswiatApiBaseUrl = new Uri("https://articles.example/wp-json/"),
            },
            new InMemoryTransientContentCache()
        );

        await service.GetLatestItemsAsync(5);
        await service.GetLatestItemsAsync(5);

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

    private static HttpResponseMessage CreatePagedPostsResponse(
        HttpRequestMessage request,
        params (int Id, string Date, string Title, string Link)[] posts
    )
    {
        var page = GetQueryInt(request.RequestUri!, "page", 1);
        var perPage = GetQueryInt(request.RequestUri!, "per_page", 10);
        var items = posts
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(post => new
            {
                id = post.Id,
                date = post.Date,
                link = post.Link,
                title = new { rendered = post.Title },
                excerpt = new { rendered = "<p>Opis</p>" },
            })
            .ToArray();

        var totalPages = Math.Max(1, (int)Math.Ceiling(posts.Length / (double)perPage));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers =
            {
                { "X-WP-TotalPages", totalPages.ToString() },
            },
            Content = new StringContent(JsonSerializer.Serialize(items), Encoding.UTF8, "application/json"),
        };
    }

    private static int GetQueryInt(Uri uri, string key, int fallbackValue)
    {
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = part.Split('=', 2);
            if (
                segments.Length == 2
                && string.Equals(segments[0], key, StringComparison.Ordinal)
                && int.TryParse(segments[1], out var parsed)
            )
            {
                return parsed;
            }
        }

        return fallbackValue;
    }

    private static (int Id, string Date, string Title, string Link) CreatePost(
        int id,
        string date,
        string title,
        string link
    ) => (id, date, title, link);
}
