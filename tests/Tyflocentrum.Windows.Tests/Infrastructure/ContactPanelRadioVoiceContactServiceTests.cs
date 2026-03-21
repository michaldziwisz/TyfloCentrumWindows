using System.Net;
using System.Text;
using Tyflocentrum.Windows.Infrastructure.Http;
using Xunit;

namespace Tyflocentrum.Windows.Tests.Infrastructure;

public sealed class ContactPanelRadioVoiceContactServiceTests
{
    [Fact]
    public async Task SendVoiceMessageAsync_posts_multipart_payload_to_addvoice_endpoint()
    {
        Uri? requestedUri = null;
        HttpMethod? method = null;
        string? contentType = null;
        string? body = null;
        var tempFilePath = Path.GetTempFileName();

        try
        {
            await File.WriteAllBytesAsync(tempFilePath, Encoding.UTF8.GetBytes("voice"));

            var handler = new StubHttpMessageHandler(request =>
            {
                requestedUri = request.RequestUri;
                method = request.Method;
                contentType = request.Content?.Headers.ContentType?.MediaType;
                body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"author":"Jan","duration_ms":4200,"error":null}""",
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            });

            using var httpClient = new HttpClient(handler);
            var service = new ContactPanelRadioVoiceContactService(
                httpClient,
                new TyflocentrumEndpointsOptions
                {
                    ContactPanelBaseUrl = new Uri("https://kontakt.example/json.php"),
                }
            );

            var result = await service.SendVoiceMessageAsync(" Jan ", tempFilePath, 4200);

            Assert.True(result.Success);
            Assert.Equal(4200, result.DurationMs);
            Assert.Equal(HttpMethod.Post, method);
            Assert.Equal("multipart/form-data", contentType);
            Assert.NotNull(requestedUri);
            Assert.Contains("ac=addvoice", requestedUri!.Query);
            Assert.Contains("author", body, StringComparison.Ordinal);
            Assert.Contains("Jan", body, StringComparison.Ordinal);
            Assert.Contains("duration_ms", body, StringComparison.Ordinal);
            Assert.Contains("4200", body, StringComparison.Ordinal);
            Assert.Contains("audio", body, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task SendVoiceMessageAsync_returns_api_error_message()
    {
        var tempFilePath = Path.GetTempFileName();

        try
        {
            await File.WriteAllBytesAsync(tempFilePath, Encoding.UTF8.GetBytes("voice"));

            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"author":"Jan","duration_ms":4200,"error":"Błąd wysyłki"}""",
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );

            using var httpClient = new HttpClient(handler);
            var service = new ContactPanelRadioVoiceContactService(
                httpClient,
                new TyflocentrumEndpointsOptions()
            );

            var result = await service.SendVoiceMessageAsync("Jan", tempFilePath, 4200);

            Assert.False(result.Success);
            Assert.Equal("Błąd wysyłki", result.ErrorMessage);
            Assert.Equal(4200, result.DurationMs);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
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
