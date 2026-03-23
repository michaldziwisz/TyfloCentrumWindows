using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class PushNotificationRegistrationSyncService : IPushNotificationRegistrationSyncService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public PushNotificationRegistrationSyncService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options
    )
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task RegisterAsync(
        string token,
        string env,
        PushNotificationPreferences preferences,
        CancellationToken cancellationToken = default
    )
    {
        var body = new RegisterBody(token, env, preferences);
        var url = new Uri(_options.PushServiceBaseUrl, "api/v1/register");
        using var response = await _httpClient.PostAsJsonAsync(
            url,
            body,
            SerializerOptions,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    public async Task UnregisterAsync(string token, CancellationToken cancellationToken = default)
    {
        var url = new Uri(_options.PushServiceBaseUrl, "api/v1/unregister");
        using var response = await _httpClient.PostAsJsonAsync(
            url,
            new UnregisterBody(token),
            SerializerOptions,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    private sealed record RegisterBody(
        string Token,
        string Env,
        PushNotificationPreferences Prefs
    );

    private sealed record UnregisterBody(string Token);
}
