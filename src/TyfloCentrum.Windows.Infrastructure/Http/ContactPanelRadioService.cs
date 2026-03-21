using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class ContactPanelRadioService : IRadioService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public ContactPanelRadioService(HttpClient httpClient, TyfloCentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public Uri LiveStreamUrl => _options.TyfloradioStreamUrl;

    public async Task<RadioAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var builder = new UriBuilder(_options.ContactPanelBaseUrl)
        {
            Query = "ac=current",
        };

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var item = await response.Content.ReadFromJsonAsync<RadioAvailability>(
            SerializerOptions,
            cancellationToken
        );

        return item ?? new RadioAvailability(false, null);
    }

    public async Task<RadioScheduleInfo> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        var builder = new UriBuilder(_options.ContactPanelBaseUrl)
        {
            Query = "ac=schedule",
        };

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var item = await response.Content.ReadFromJsonAsync<RadioScheduleInfo>(
            SerializerOptions,
            cancellationToken
        );

        return item ?? new RadioScheduleInfo(false, null, null);
    }
}
