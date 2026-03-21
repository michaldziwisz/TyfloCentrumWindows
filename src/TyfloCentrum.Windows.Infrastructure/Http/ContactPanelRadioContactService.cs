using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class ContactPanelRadioContactService : IRadioContactService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public ContactPanelRadioContactService(HttpClient httpClient, TyfloCentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<ContactSubmissionResult> SendMessageAsync(
        string author,
        string comment,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(_options.ContactPanelBaseUrl)
        {
            Query = "ac=add",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, builder.Uri)
        {
            Content = JsonContent.Create(
                new ContactPanelRequest(author.Trim(), comment),
                options: SerializerOptions
            ),
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ContactPanelResponse>(
            SerializerOptions,
            cancellationToken
        );

        return string.IsNullOrWhiteSpace(payload?.Error)
            ? new ContactSubmissionResult(true, null)
            : new ContactSubmissionResult(false, payload.Error);
    }

    private sealed record ContactPanelRequest(string Author, string Comment);

    private sealed record ContactPanelResponse(string? Author, string? Comment, string? Error);
}
