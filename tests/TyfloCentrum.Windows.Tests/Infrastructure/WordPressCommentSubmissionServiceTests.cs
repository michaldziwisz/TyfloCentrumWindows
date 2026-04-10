using System.Net;
using System.Text;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class WordPressCommentSubmissionServiceTests
{
    [Fact]
    public async Task SubmitCommentAsync_posts_legacy_comment_form_and_returns_published_result()
    {
        string? capturedPostBody = null;
        HttpRequestMessage? capturedPostRequest = null;

        var handler = new StubHttpMessageHandler(async request =>
        {
            if (
                request.RequestUri?.AbsoluteUri
                == "https://podcasts.example/wp-json/wp/v2/posts/77?_fields=link"
            )
            {
                return JsonResponse("""{ "link": "https://podcasts.example/posts/77/" }""");
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/posts/77/")
            {
                return HtmlResponse(
                    """
                        <html>
                          <body>
                        <form action="https://podcasts.example/wp-comments-post.php" method="post" id="commentform">
                          <input type="hidden" name="akismet_comment_nonce" value="nonce123" />
                        </form>
                      </body>
                    </html>
                    """
                );
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/wp-comments-post.php")
            {
                capturedPostRequest = request;
                capturedPostBody = await request.Content!.ReadAsStringAsync();

                return new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers =
                    {
                        Location = new Uri("https://podcasts.example/posts/77/#comment-2001"),
                    },
                };
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        using var httpClient = CreateHttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.SubmitCommentAsync(
            new WordPressCommentSubmissionRequest(
                77,
                "Jan",
                "jan@example.com",
                "Treść komentarza"
            )
        );

        Assert.True(result.Accepted);
        Assert.Equal(WordPressCommentSubmissionOutcome.Published, result.Outcome);
        Assert.Equal("Komentarz został opublikowany.", result.Message);
        Assert.NotNull(capturedPostRequest);
        Assert.Equal(HttpMethod.Post, capturedPostRequest!.Method);
        Assert.Equal(
            "https://podcasts.example/wp-comments-post.php",
            capturedPostRequest.RequestUri!.AbsoluteUri
        );
        Assert.Contains("TyfloCentrum.Windows.App/", capturedPostRequest.Headers.UserAgent.ToString());
        Assert.Equal("https://podcasts.example/posts/77/", capturedPostRequest.Headers.Referrer?.AbsoluteUri);
        Assert.NotNull(capturedPostBody);
        Assert.Contains("comment=Tre%C5%9B%C4%87+komentarza", capturedPostBody);
        Assert.Contains("author=Jan", capturedPostBody);
        Assert.Contains("email=jan%40example.com", capturedPostBody);
        Assert.Contains("comment_post_ID=77", capturedPostBody);
        Assert.Contains("comment_parent=0", capturedPostBody);
        Assert.Contains("akismet_comment_nonce=nonce123", capturedPostBody);
        Assert.Contains("ak_hp_textarea=", capturedPostBody);
        Assert.Contains("ak_js=", capturedPostBody);
    }

    [Fact]
    public async Task SubmitCommentAsync_maps_unapproved_redirect_to_pending_moderation()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (
                request.RequestUri?.AbsoluteUri
                == "https://podcasts.example/wp-json/wp/v2/posts/77?_fields=link"
            )
            {
                return Task.FromResult(
                    JsonResponse("""{ "link": "https://podcasts.example/posts/77/" }""")
                );
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/posts/77/")
            {
                return Task.FromResult(
                    HtmlResponse("""<form action="https://podcasts.example/wp-comments-post.php" method="post" id="commentform"></form>""")
                );
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/wp-comments-post.php")
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.Redirect)
                    {
                        Headers =
                        {
                            Location = new Uri(
                                "https://podcasts.example/posts/77/?unapproved=2002&moderation-hash=abc#comment-2002"
                            ),
                        },
                    }
                );
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        using var httpClient = CreateHttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.SubmitCommentAsync(
            new WordPressCommentSubmissionRequest(
                77,
                "Jan",
                "jan@example.com",
                "Treść komentarza",
                ParentId: 1001
            )
        );

        Assert.True(result.Accepted);
        Assert.Equal(WordPressCommentSubmissionOutcome.PendingModeration, result.Outcome);
        Assert.Equal("Komentarz został przekazany do moderacji.", result.Message);
    }

    [Fact]
    public async Task SubmitCommentAsync_maps_spam_error_page_to_spam_result()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (
                request.RequestUri?.AbsoluteUri
                == "https://podcasts.example/wp-json/wp/v2/posts/77?_fields=link"
            )
            {
                return Task.FromResult(
                    JsonResponse("""{ "link": "https://podcasts.example/posts/77/" }""")
                );
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/posts/77/")
            {
                return Task.FromResult(
                    HtmlResponse("""<form action="https://podcasts.example/wp-comments-post.php" method="post" id="commentform"></form>""")
                );
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/wp-comments-post.php")
            {
                return Task.FromResult(
                    HtmlResponse(
                        """
                        <html>
                          <div class="wp-die-message">Komentarz został oznaczony jako spam.</div>
                        </html>
                        """
                    )
                );
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        using var httpClient = CreateHttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.SubmitCommentAsync(
            new WordPressCommentSubmissionRequest(
                77,
                "Jan",
                "jan@example.com",
                "Treść komentarza"
            )
        );

        Assert.True(result.Accepted);
        Assert.Equal(WordPressCommentSubmissionOutcome.Spam, result.Outcome);
        Assert.Equal("Komentarz został zakwalifikowany jako spam.", result.Message);
    }

    [Fact]
    public async Task SubmitCommentAsync_returns_wordpress_error_message_from_legacy_form()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (
                request.RequestUri?.AbsoluteUri
                == "https://podcasts.example/wp-json/wp/v2/posts/77?_fields=link"
            )
            {
                return Task.FromResult(
                    JsonResponse("""{ "link": "https://podcasts.example/posts/77/" }""")
                );
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/posts/77/")
            {
                return Task.FromResult(
                    HtmlResponse("""<form action="https://podcasts.example/wp-comments-post.php" method="post" id="commentform"></form>""")
                );
            }

            if (request.RequestUri?.AbsoluteUri == "https://podcasts.example/wp-comments-post.php")
            {
                return Task.FromResult(
                    HtmlResponse(
                        """
                        <html>
                          <div class="wp-die-message">Błąd: proszę wpisać komentarz.</div>
                        </html>
                        """
                    )
                );
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        using var httpClient = CreateHttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.SubmitCommentAsync(
            new WordPressCommentSubmissionRequest(
                77,
                "Jan",
                "jan@example.com",
                "Treść komentarza"
            )
        );

        Assert.False(result.Accepted);
        Assert.Equal(WordPressCommentSubmissionOutcome.Rejected, result.Outcome);
        Assert.Equal("Błąd: proszę wpisać komentarz.", result.Message);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "TyfloCentrum.Windows.App/0.1.7 (+https://github.com/michaldziwisz/TyfloCentrumWindows)"
        );
        return httpClient;
    }

    private static WordPressCommentsService CreateService(HttpClient httpClient)
    {
        return new WordPressCommentsService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastApiBaseUrl = new Uri("https://podcasts.example/wp-json/"),
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

    private static HttpResponseMessage HtmlResponse(string html)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return handler(request);
        }
    }
}
