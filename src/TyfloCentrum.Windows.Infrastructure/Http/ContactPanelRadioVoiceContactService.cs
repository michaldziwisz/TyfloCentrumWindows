using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class ContactPanelRadioVoiceContactService : IRadioVoiceContactService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public ContactPanelRadioVoiceContactService(HttpClient httpClient, TyfloCentrumEndpointsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<VoiceMessageSubmissionResult> SendVoiceMessageAsync(
        string author,
        string filePath,
        int durationMs,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(_options.ContactPanelBaseUrl)
        {
            Query = "ac=addvoice",
        };

        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mp4");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(author.Trim()), "author");
        content.Add(new StringContent(durationMs.ToString(System.Globalization.CultureInfo.InvariantCulture)), "duration_ms");
        content.Add(fileContent, "audio", Path.GetFileName(filePath));

        using var request = new HttpRequestMessage(HttpMethod.Post, builder.Uri)
        {
            Content = content,
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VoiceContactPanelResponse>(
            SerializerOptions,
            cancellationToken
        );

        return string.IsNullOrWhiteSpace(payload?.Error)
            ? new VoiceMessageSubmissionResult(true, null, payload?.DurationMs)
            : new VoiceMessageSubmissionResult(false, payload.Error, payload?.DurationMs);
    }

    private sealed record VoiceContactPanelResponse(
        string? Author,
        string? Error,
        [property: JsonPropertyName("duration_ms")]
        int? DurationMs
    );
}
