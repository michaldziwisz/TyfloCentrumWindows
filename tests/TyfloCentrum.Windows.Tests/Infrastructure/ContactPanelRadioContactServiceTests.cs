using System.Net;
using System.Text;
using TyfloCentrum.Windows.Infrastructure.Http;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class ContactPanelRadioContactServiceTests
{
    [Fact]
    public async Task SendMessageAsync_posts_json_to_add_endpoint()
    {
        Uri? requestedUri = null;
        HttpMethod? method = null;
        string? contentType = null;
        string? body = null;

        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            method = request.Method;
            contentType = request.Content?.Headers.ContentType?.MediaType;
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"author":"Jan","comment":"Test","error":null}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new ContactPanelRadioContactService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                ContactPanelBaseUrl = new Uri("https://kontakt.example/json.php"),
            }
        );

        var result = await service.SendMessageAsync(" Jan ", "Test");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("application/json", contentType);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://kontakt.example/json.php", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("ac=add", requestedUri.Query);
        Assert.Contains(@"""author"":""Jan""", body, StringComparison.Ordinal);
        Assert.Contains(@"""comment"":""Test""", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMessageAsync_returns_api_error_message()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"author":"Jan","comment":"Test","error":"Błąd wysyłki"}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );

        using var httpClient = new HttpClient(handler);
        var service = new ContactPanelRadioContactService(
            httpClient,
            new TyfloCentrumEndpointsOptions()
        );

        var result = await service.SendMessageAsync("Jan", "Test");

        Assert.False(result.Success);
        Assert.Equal("Błąd wysyłki", result.ErrorMessage);
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
