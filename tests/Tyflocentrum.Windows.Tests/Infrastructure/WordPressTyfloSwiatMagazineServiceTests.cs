using System.Net;
using System.Text;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Infrastructure.Http;
using Xunit;

namespace Tyflocentrum.Windows.Tests.Infrastructure;

public sealed class WordPressTyfloSwiatMagazineServiceTests
{
    [Fact]
    public async Task GetIssuesAsync_falls_back_to_slug_lookup_when_default_root_is_empty()
    {
        var requestedUris = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);

            if (request.RequestUri!.Query.Contains("parent=1409", StringComparison.Ordinal))
            {
                return JsonResponse("[]");
            }

            if (request.RequestUri!.Query.Contains("slug=czasopismo", StringComparison.Ordinal))
            {
                return JsonResponse(
                    """
                    [
                      {
                        "id": 9999,
                        "date": "2026-03-01T12:00:00",
                        "link": "https://tyfloswiat.pl/czasopismo/",
                        "title": { "rendered": "Czasopismo TyfloŚwiat" },
                        "excerpt": { "rendered": "" }
                      }
                    ]
                    """
                );
            }

            return JsonResponse(
                """
                [
                  {
                    "id": 7772,
                    "date": "2026-03-20T12:00:00",
                    "link": "https://tyfloswiat.pl/czasopismo/tyfloswiat-1-2026/",
                    "title": { "rendered": "Tyfloświat 1/2026" },
                    "excerpt": { "rendered": "<p>Numer</p>" }
                  }
                ]
                """
            );
        });

        using var client = new HttpClient(handler);
        var service = new WordPressTyfloSwiatMagazineService(
            client,
            new TyflocentrumEndpointsOptions
            {
                TyfloswiatApiBaseUrl = new Uri("https://tyfloswiat.example/wp-json/"),
            }
        );

        var issues = await service.GetIssuesAsync();

        var issue = Assert.Single(issues);
        Assert.Equal(7772, issue.Id);
        Assert.Contains(requestedUris, uri => uri.Query.Contains("parent=1409", StringComparison.Ordinal));
        Assert.Contains(requestedUris, uri => uri.Query.Contains("slug=czasopismo", StringComparison.Ordinal));
        Assert.Contains(requestedUris, uri => uri.Query.Contains("parent=9999", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetIssueAsync_extracts_pdf_and_orders_toc_by_links_from_issue_html()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/wp/v2/pages/7772", StringComparison.Ordinal))
            {
                return JsonResponse(
                    """
                    {
                      "id": 7772,
                      "date": "2026-03-20T12:00:00",
                      "link": "https://tyfloswiat.pl/czasopismo/tyfloswiat-1-2026/",
                      "title": { "rendered": "Tyfloświat 1/2026" },
                      "excerpt": { "rendered": "" },
                      "content": {
                        "rendered": "<h2>Spis treści</h2><ul><li><a href='https://tyfloswiat.pl/czasopismo/tyfloswiat-1-2026/artykul-2/'>Artykuł 2</a></li><li><a href='https://tyfloswiat.pl/czasopismo/tyfloswiat-1-2026/artykul-1/'>Artykuł 1</a></li></ul><p><a href='https://tyfloswiat.pl/wp-content/uploads/2026/03/Tyflo-1_2026.pdf'>PDF</a></p>"
                      },
                      "guid": { "rendered": "https://tyfloswiat.pl/?page_id=7772" }
                    }
                    """
                );
            }

            return JsonResponse(
                """
                [
                  {
                    "id": 1001,
                    "date": "2026-03-20T10:00:00",
                    "link": "https://tyfloswiat.pl/czasopismo/tyfloswiat-1-2026/artykul-1/",
                    "title": { "rendered": "Artykuł 1" },
                    "excerpt": { "rendered": "<p>A1</p>" }
                  },
                  {
                    "id": 1002,
                    "date": "2026-03-20T11:00:00",
                    "link": "https://tyfloswiat.pl/czasopismo/tyfloswiat-1-2026/artykul-2/",
                    "title": { "rendered": "Artykuł 2" },
                    "excerpt": { "rendered": "<p>A2</p>" }
                  }
                ]
                """
            );
        });

        using var client = new HttpClient(handler);
        var service = new WordPressTyfloSwiatMagazineService(
            client,
            new TyflocentrumEndpointsOptions
            {
                TyfloswiatApiBaseUrl = new Uri("https://tyfloswiat.example/wp-json/"),
            }
        );

        var issue = await service.GetIssueAsync(7772);

        Assert.Equal(
            "https://tyfloswiat.pl/wp-content/uploads/2026/03/Tyflo-1_2026.pdf",
            issue.PdfUrl
        );
        Assert.Collection(
            issue.TocItems,
            item => Assert.Equal(1002, item.Id),
            item => Assert.Equal(1001, item.Id)
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
