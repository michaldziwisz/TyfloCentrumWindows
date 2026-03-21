using System.Net;
using System.Text;
using TyfloCentrum.Windows.Infrastructure.Http;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class ContactPanelRadioServiceTests
{
    [Fact]
    public async Task GetAvailabilityAsync_requests_current_action()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    { "available": true, "title": "Audycja testowa" }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new ContactPanelRadioService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                ContactPanelBaseUrl = new Uri("https://kontakt.example/json.php"),
                TyfloradioStreamUrl = new Uri("https://radio.example/live.m3u8"),
            }
        );

        var availability = await service.GetAvailabilityAsync();

        Assert.True(availability.Available);
        Assert.Equal("Audycja testowa", availability.Title);
        Assert.NotNull(requestedUri);
        Assert.Equal("https://kontakt.example/json.php", requestedUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("ac=current", requestedUri.Query);
        Assert.Equal("https://radio.example/live.m3u8", service.LiveStreamUrl.ToString());
    }

    [Fact]
    public async Task GetScheduleAsync_requests_schedule_action()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    { "available": true, "text": "12:00 Test", "error": null }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        using var httpClient = new HttpClient(handler);
        var service = new ContactPanelRadioService(
            httpClient,
            new TyfloCentrumEndpointsOptions
            {
                ContactPanelBaseUrl = new Uri("https://kontakt.example/json.php"),
            }
        );

        var schedule = await service.GetScheduleAsync();

        Assert.True(schedule.Available);
        Assert.Equal("12:00 Test", schedule.Text);
        Assert.NotNull(requestedUri);
        Assert.Contains("ac=schedule", requestedUri!.Query);
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
