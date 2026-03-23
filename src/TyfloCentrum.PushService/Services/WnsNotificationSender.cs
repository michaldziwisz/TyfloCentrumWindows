using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TyfloCentrum.PushService.Models;

namespace TyfloCentrum.PushService.Services;

public sealed class WnsNotificationSender : IWnsNotificationSender
{
    private readonly HttpClient _httpClient;
    private readonly WnsAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<WnsNotificationSender> _logger;

    public WnsNotificationSender(
        HttpClient httpClient,
        WnsAccessTokenProvider accessTokenProvider,
        ILogger<WnsNotificationSender> logger
    )
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
    }

    public async Task<PushSendResult> SendToastAsync(
        string channelUri,
        PushDispatchPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        if (!_accessTokenProvider.IsConfigured)
        {
            _logger.LogWarning("Skipping WNS delivery because Azure credentials are not configured.");
            return PushSendResult.Failed();
        }

        return await SendCoreAsync(channelUri, payload, retryOnUnauthorized: true, cancellationToken);
    }

    private async Task<PushSendResult> SendCoreAsync(
        string channelUri,
        PushDispatchPayload payload,
        bool retryOnUnauthorized,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return PushSendResult.Failed();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, channelUri)
        {
            Content = new StringContent(WnsToastXmlBuilder.Build(payload), Encoding.UTF8, "text/xml"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-WNS-Type", "wns/toast");
        request.Headers.TryAddWithoutValidation("X-WNS-RequestForStatus", "true");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return PushSendResult.Delivered();
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized && retryOnUnauthorized)
        {
            _accessTokenProvider.Invalidate();
            return await SendCoreAsync(channelUri, payload, retryOnUnauthorized: false, cancellationToken);
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushSendResult.InvalidChannel((int)response.StatusCode);
        }

        var description = response.Headers.TryGetValues("X-WNS-Error-Description", out var values)
            ? string.Join(", ", values)
            : "unknown";

        _logger.LogWarning(
            "WNS delivery failed. Status code: {StatusCode}. Description: {Description}.",
            (int)response.StatusCode,
            description
        );

        return PushSendResult.Failed((int)response.StatusCode);
    }
}
