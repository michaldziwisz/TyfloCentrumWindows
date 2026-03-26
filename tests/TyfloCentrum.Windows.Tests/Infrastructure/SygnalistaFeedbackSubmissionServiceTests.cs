using System.Net;
using System.Text;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class SygnalistaFeedbackSubmissionServiceTests
{
    [Fact]
    public async Task SubmitAsync_sends_user_agent_and_private_log_without_email()
    {
        string? capturedUrl = null;
        string? capturedUserAgent = null;
        string? capturedAppToken = null;
        string? capturedPayload = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedUrl = request.RequestUri!.ToString();
            capturedUserAgent = request.Headers.TryGetValues("User-Agent", out var userAgents)
                ? userAgents.Single()
                : null;
            capturedAppToken = request.Headers.TryGetValues("x-sygnalista-app-token", out var appTokens)
                ? appTokens.Single()
                : null;
            capturedPayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """
                    {
                      "ok": true,
                      "reportId": "rep-123",
                      "issue": {
                        "number": 17,
                        "url": "https://api.github.com/repos/example/repo/issues/17",
                        "html_url": "https://github.com/example/repo/issues/17"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://sygnalista.example/"),
        };
        var service = new SygnalistaFeedbackSubmissionService(
            client,
            new TyfloCentrumEndpointsOptions
            {
                SygnalistaBaseUrl = new Uri("https://sygnalista.example/"),
                SygnalistaAppId = "tyflocentrum",
                SygnalistaAppToken = "secret-token",
            },
            new FakeFeedbackDiagnosticsCollector()
        );

        var result = await service.SubmitAsync(
            new FeedbackSubmissionRequest(
                FeedbackSubmissionKind.Bug,
                "Błąd startu",
                "Po wejściu do sekcji słychać dwa komunikaty.",
                "michal@example.com",
                true,
                true
            )
        );

        Assert.True(result.Success);
        Assert.Equal("https://sygnalista.example/v1/report", capturedUrl);
        Assert.Equal("TyfloCentrum.Windows.App/0.1.3.0", capturedUserAgent);
        Assert.Equal("secret-token", capturedAppToken);

        using var json = JsonDocument.Parse(capturedPayload!);
        var root = json.RootElement;
        Assert.Equal("michal@example.com", root.GetProperty("email").GetString());
        Assert.Equal("bug", root.GetProperty("kind").GetString());
        Assert.Equal("tyflocentrum", root.GetProperty("app").GetProperty("id").GetString());
        Assert.True(root.TryGetProperty("diagnostics", out _));
        Assert.True(root.TryGetProperty("logs", out _));
    }

    [Fact]
    public async Task SubmitAsync_omits_optional_payload_parts_when_not_requested()
    {
        string? capturedPayload = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedPayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """
                    {
                      "ok": true,
                      "reportId": "rep-321",
                      "issue": {
                        "number": 18,
                        "url": "https://api.github.com/repos/example/repo/issues/18",
                        "html_url": "https://github.com/example/repo/issues/18"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });
        var service = new SygnalistaFeedbackSubmissionService(
            new HttpClient(handler),
            new TyfloCentrumEndpointsOptions
            {
                SygnalistaBaseUrl = new Uri("https://sygnalista.example/"),
                SygnalistaAppId = "tyflocentrum",
            },
            new FakeFeedbackDiagnosticsCollector()
        );

        await service.SubmitAsync(
            new FeedbackSubmissionRequest(
                FeedbackSubmissionKind.Suggestion,
                "Lepsza etykieta",
                "Przydałby się krótszy opis przycisku.",
                null,
                false,
                false
            )
        );

        using var json = JsonDocument.Parse(capturedPayload!);
        var root = json.RootElement;
        Assert.Equal("suggestion", root.GetProperty("kind").GetString());
        Assert.False(root.TryGetProperty("email", out _));
        Assert.False(root.TryGetProperty("diagnostics", out _));
        Assert.False(root.TryGetProperty("logs", out _));
    }

    [Fact]
    public async Task SubmitAsync_returns_friendly_message_when_server_does_not_know_app_id()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """
                    {
                      "error": {
                        "code": "bad_request",
                        "message": "Unknown app.id: tyflocentrum"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );
        var service = new SygnalistaFeedbackSubmissionService(
            new HttpClient(handler),
            new TyfloCentrumEndpointsOptions
            {
                SygnalistaBaseUrl = new Uri("https://sygnalista.example/"),
                SygnalistaAppId = "tyflocentrum",
            },
            new FakeFeedbackDiagnosticsCollector()
        );

        var result = await service.SubmitAsync(
            new FeedbackSubmissionRequest(
                FeedbackSubmissionKind.Bug,
                "Błąd zgłoszenia",
                "Opis testowy.",
                null,
                false,
                false
            )
        );

        Assert.False(result.Success);
        Assert.Equal(
            "Obsługa zgłoszeń jest jeszcze włączana po stronie serwera. Spróbuj ponownie później.",
            result.ErrorMessage
        );
    }

    private sealed class FakeFeedbackDiagnosticsCollector : IFeedbackDiagnosticsCollector
    {
        public Task<FeedbackDiagnosticsSnapshot> CollectAsync(
            bool includeDiagnostics,
            bool includeLogFile,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(
                new FeedbackDiagnosticsSnapshot(
                    "0.1.3.0",
                    "0.1.3.0",
                    "stable",
                    "TyfloCentrum.Windows.App/0.1.3.0",
                    includeDiagnostics
                        ? new Dictionary<string, object?> { ["app"] = "TyfloCentrum" }
                        : new Dictionary<string, object?>(),
                    includeLogFile
                        ? new FeedbackLogAttachment(
                            "tyflocentrum-current.log.gz",
                            "application/gzip",
                            "base64",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
                            4,
                            false
                        )
                        : null
                )
            );
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }
}
