using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Options;
using Microsoft.Extensions.Options;

namespace TyfloCentrum.PushService.Services;

public sealed class WnsAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly PushServiceOptions _options;
    private readonly ILogger<WnsAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WnsAccessToken? _cachedToken;

    public WnsAccessTokenProvider(
        HttpClient httpClient,
        IOptions<PushServiceOptions> options,
        ILogger<WnsAccessTokenProvider> logger
    )
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.AzureTenantId)
        && !string.IsNullOrWhiteSpace(_options.AzureClientId)
        && !string.IsNullOrWhiteSpace(_options.AzureClientSecret);

    public void Invalidate()
    {
        _cachedToken = null;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var nowUtc = DateTimeOffset.UtcNow;
            if (_cachedToken is not null && _cachedToken.IsValid(nowUtc.AddMinutes(1)))
            {
                return _cachedToken.Value;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.AzureTenantId)}/oauth2/v2.0/token"
            )
            {
                Content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["client_id"] = _options.AzureClientId,
                        ["client_secret"] = _options.AzureClientSecret,
                        ["grant_type"] = "client_credentials",
                        ["scope"] = _options.WnsScope,
                    }
                ),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to obtain WNS access token. Status code: {StatusCode}.",
                    (int)response.StatusCode
                );
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                _logger.LogWarning("WNS token endpoint returned an empty access token.");
                return null;
            }

            _cachedToken = new WnsAccessToken(
                payload.AccessToken,
                nowUtc.AddSeconds(Math.Max(60, payload.ExpiresIn))
            );

            return _cachedToken.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
