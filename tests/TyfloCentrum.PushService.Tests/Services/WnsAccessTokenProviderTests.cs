using System.Net;
using System.Text;
using TyfloCentrum.PushService.Options;
using TyfloCentrum.PushService.Services;
using TyfloCentrum.PushService.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace TyfloCentrum.PushService.Tests.Services;

public sealed class WnsAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_requests_token_and_caches_value()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                callCount += 1;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"token-1","expires_in":3600}""",
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
        );
        var client = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(
            new PushServiceOptions
            {
                AzureTenantId = "tenant",
                AzureClientId = "client",
                AzureClientSecret = "secret",
            }
        );

        var provider = new WnsAccessTokenProvider(client, options, NullLogger<WnsAccessTokenProvider>.Instance);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        Assert.Equal(1, callCount);
    }
}
