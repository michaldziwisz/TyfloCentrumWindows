using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed partial class SygnalistaFeedbackSubmissionService : IFeedbackSubmissionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IFeedbackDiagnosticsCollector _diagnosticsCollector;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public SygnalistaFeedbackSubmissionService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        IFeedbackDiagnosticsCollector diagnosticsCollector
    )
    {
        _httpClient = httpClient;
        _options = options;
        _diagnosticsCollector = diagnosticsCollector;
    }

    public async Task<FeedbackSubmissionResult> SubmitAsync(
        FeedbackSubmissionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (_options.SygnalistaBaseUrl is null)
        {
            return new FeedbackSubmissionResult(
                false,
                "Obsługa zgłoszeń nie jest jeszcze skonfigurowana w tej wersji aplikacji.",
                null,
                null
            );
        }

        var diagnostics = await _diagnosticsCollector.CollectAsync(
            request.IncludeDiagnostics,
            request.IncludeLogFile,
            cancellationToken
        );

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.SygnalistaBaseUrl, "v1/report")
        )
        {
            Content = JsonContent.Create(
                new ReportRequest(
                    new AppDescriptor(
                        _options.SygnalistaAppId,
                        diagnostics.AppVersion,
                        diagnostics.Build,
                        diagnostics.Channel
                    ),
                    request.Kind == FeedbackSubmissionKind.Bug ? "bug" : "suggestion",
                    request.Title.Trim(),
                    request.Description.Trim(),
                    string.IsNullOrWhiteSpace(request.ContactEmail)
                        ? null
                        : request.ContactEmail.Trim(),
                    request.IncludeDiagnostics ? diagnostics.PublicDiagnostics : null,
                    request.IncludeLogFile ? diagnostics.LogAttachment : null
                ),
                options: SerializerOptions
            ),
        };

        message.Headers.TryAddWithoutValidation("User-Agent", diagnostics.UserAgent);

        if (!string.IsNullOrWhiteSpace(_options.SygnalistaAppToken))
        {
            message.Headers.TryAddWithoutValidation(
                "x-sygnalista-app-token",
                _options.SygnalistaAppToken
            );
        }

        using var response = await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (TryBuildSuccessResult(responseBody, response.IsSuccessStatusCode, out var result))
        {
            return result;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = TryReadError(response, responseBody);
            return new FeedbackSubmissionResult(false, error, null, null);
        }

        return new FeedbackSubmissionResult(true, null, null, null);
    }

    private static bool TryBuildSuccessResult(
        string responseBody,
        bool isSuccessStatusCode,
        out FeedbackSubmissionResult result
    )
    {
        var payload = TryDeserialize<ReportResponse>(responseBody);
        if (!string.IsNullOrWhiteSpace(payload?.Issue?.PublicUrl))
        {
            result = new FeedbackSubmissionResult(
                true,
                null,
                payload.Issue.PublicUrl,
                payload.ReportId
            );
            return true;
        }

        if (payload?.Ok == true)
        {
            result = new FeedbackSubmissionResult(true, null, null, payload.ReportId);
            return true;
        }

        var publicIssueUrl = ExtractPublicIssueUrl(responseBody);
        if (!string.IsNullOrWhiteSpace(publicIssueUrl))
        {
            result = new FeedbackSubmissionResult(true, null, publicIssueUrl, payload?.ReportId);
            return true;
        }

        if (isSuccessStatusCode)
        {
            result = new FeedbackSubmissionResult(true, null, null, payload?.ReportId);
            return true;
        }

        result = new FeedbackSubmissionResult(
            false,
            "Serwer zgłoszeń zwrócił nieprawidłową odpowiedź.",
            null,
            null
        );
        return false;
    }

    private static string TryReadError(HttpResponseMessage response, string responseBody)
    {
        var payload = TryDeserialize<ErrorEnvelope>(responseBody);
        if (!string.IsNullOrWhiteSpace(payload?.Error?.Message))
        {
            if (payload.Error.Message.StartsWith("Unknown app.id:", StringComparison.Ordinal))
            {
                return "Obsługa zgłoszeń jest jeszcze włączana po stronie serwera. Spróbuj ponownie później.";
            }

            return payload.Error.Message;
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "Serwer zgłoszeń odrzucił żądanie autoryzacyjne.",
            System.Net.HttpStatusCode.TooManyRequests =>
                "Wysłano zbyt wiele zgłoszeń w krótkim czasie. Spróbuj ponownie za chwilę.",
            _ => "Nie udało się wysłać zgłoszenia. Spróbuj ponownie później.",
        };
    }

    private static T? TryDeserialize<T>(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(responseBody, SerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? ExtractPublicIssueUrl(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        var match = PublicIssueUrlRegex().Match(responseBody);
        return match.Success ? match.Value : null;
    }

    private sealed record AppDescriptor(
        string Id,
        string Version,
        string Build,
        string? Channel
    );

    private sealed record ReportRequest(
        AppDescriptor App,
        string Kind,
        string Title,
        string Description,
        string? Email,
        IReadOnlyDictionary<string, object?>? Diagnostics,
        FeedbackLogAttachment? Logs
    );

    private sealed record ReportResponse(bool Ok, string? ReportId, IssueResponse? Issue);

    private sealed record IssueResponse(
        int Number,
        string? Url,
        [property: System.Text.Json.Serialization.JsonPropertyName("html_url")] string? HtmlUrl,
        [property: System.Text.Json.Serialization.JsonPropertyName("htmlUrl")] string? HtmlUrlCamelCase
    )
    {
        public string? PublicUrl => !string.IsNullOrWhiteSpace(HtmlUrl) ? HtmlUrl : HtmlUrlCamelCase;
    }

    private sealed record ErrorEnvelope(ErrorBody? Error);

    private sealed record ErrorBody(string? Code, string? Message);

    [GeneratedRegex(@"https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/issues/[0-9]+", RegexOptions.Compiled)]
    private static partial Regex PublicIssueUrlRegex();
}
