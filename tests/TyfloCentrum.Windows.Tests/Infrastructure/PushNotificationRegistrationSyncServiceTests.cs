using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class PushNotificationRegistrationSyncServiceTests
{
    [Fact]
    public async Task RegisterAsync_posts_expected_payload()
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler);
        var service = new PushNotificationRegistrationSyncService(
            client,
            new TyfloCentrumEndpointsOptions
            {
                PushServiceBaseUrl = new Uri("https://push.example.invalid/"),
            }
        );

        await service.RegisterAsync(
            "wns-token",
            "windows-wns",
            new PushNotificationPreferences(true, false, false, false)
        );

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://push.example.invalid/api/v1/register", handler.Request.RequestUri!.ToString());

        var body = await handler.Request.Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("wns-token", body.GetProperty("token").GetString());
        Assert.Equal("windows-wns", body.GetProperty("env").GetString());
        Assert.True(body.GetProperty("prefs").GetProperty("podcast").GetBoolean());
        Assert.False(body.GetProperty("prefs").GetProperty("article").GetBoolean());
        Assert.False(body.GetProperty("prefs").GetProperty("live").GetBoolean());
        Assert.False(body.GetProperty("prefs").GetProperty("schedule").GetBoolean());
    }

    [Fact]
    public async Task UnregisterAsync_posts_expected_payload()
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler);
        var service = new PushNotificationRegistrationSyncService(
            client,
            new TyfloCentrumEndpointsOptions
            {
                PushServiceBaseUrl = new Uri("https://push.example.invalid/"),
            }
        );

        await service.UnregisterAsync("wns-token");

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(
            "https://push.example.invalid/api/v1/unregister",
            handler.Request.RequestUri!.ToString()
        );

        var body = await handler.Request.Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("wns-token", body.GetProperty("token").GetString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
