using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressCommentsService : IWordPressCommentsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex CommentFormActionRegex = new(
        """<form(?=[^>]*id=["']commentform["'])(?=[^>]*action=["'](?<action>[^"']+)["'])[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
    );
    private static readonly Regex AkismetNonceRegex = new(
        """name=["']akismet_comment_nonce["'][^>]*value=["'](?<value>[^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
    );
    private static readonly Regex ErrorMessageRegex = new(
        """<div class=["']wp-die-message["']>(?<message>.*?)</div>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
    );
    private static readonly Regex HtmlTagRegex = new(
        """<.*?>""",
        RegexOptions.Singleline | RegexOptions.Compiled
    );

    private readonly ITransientContentCache _cache;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressCommentsService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        ITransientContentCache cache
    )
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<IReadOnlyList<WordPressComment>> GetCommentsAsync(
        int postId,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyflopodcastApiBaseUrl, "wp/v2/comments"));
        builder.Query = $"post={postId}&per_page=100";
        var cacheKey = $"wp-comments:{builder.Uri.AbsoluteUri}";

        if (forceRefresh)
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }

        return await _cache.GetOrCreateAsync(
            cacheKey,
            CacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var items = await response.Content.ReadFromJsonAsync<List<WordPressComment>>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return (IReadOnlyList<WordPressComment>)(items ?? []);
            },
            cancellationToken
        );
    }

    public async Task<WordPressCommentSubmissionResult> SubmitCommentAsync(
        WordPressCommentSubmissionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var postLink = await TryGetPostLinkAsync(request.PostId, cancellationToken);
        if (postLink is null)
        {
            return new WordPressCommentSubmissionResult(
                false,
                WordPressCommentSubmissionOutcome.Rejected,
                "Nie udało się ustalić adresu wpisu dla formularza komentarza."
            );
        }

        var formContext = await LoadCommentFormContextAsync(postLink, cancellationToken);
        if (!formContext.CanSubmit || formContext.FormActionUrl is null)
        {
            return new WordPressCommentSubmissionResult(
                false,
                WordPressCommentSubmissionOutcome.Rejected,
                formContext.ErrorMessage ?? "Nie udało się przygotować formularza komentarza."
            );
        }

        using var requestMessage = CreateHtmlRequest(HttpMethod.Post, formContext.FormActionUrl);
        requestMessage.Content = new FormUrlEncodedContent(
            BuildLegacyCommentFields(request, formContext.AkismetNonce)
        );

        requestMessage.Headers.Referrer = postLink;
        requestMessage.Headers.TryAddWithoutValidation(
            "Origin",
            $"{postLink.Scheme}://{postLink.IdnHost}"
        );

        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        return await MapLegacySubmissionResponseAsync(
            request.PostId,
            postLink,
            response,
            cancellationToken
        );
    }

    private async Task InvalidateCommentsCacheAsync(
        int postId,
        CancellationToken cancellationToken
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyflopodcastApiBaseUrl, "wp/v2/comments"));
        builder.Query = $"post={postId}&per_page=100";
        await _cache.RemoveAsync($"wp-comments:{builder.Uri.AbsoluteUri}", cancellationToken);
    }

    private async Task<Uri?> TryGetPostLinkAsync(int postId, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(_options.TyflopodcastApiBaseUrl, $"wp/v2/posts/{postId}?_fields=link");
        using var response = await _httpClient.GetAsync(
            endpoint,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<PostLinkPayload>(
            SerializerOptions,
            cancellationToken
        );

        return Uri.TryCreate(payload?.Link, UriKind.Absolute, out var postLink) ? postLink : null;
    }

    private async Task<CommentFormContext> LoadCommentFormContextAsync(
        Uri postLink,
        CancellationToken cancellationToken
    )
    {
        using var request = CreateHtmlRequest(HttpMethod.Get, postLink);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return new CommentFormContext(
                false,
                null,
                null,
                "Nie udało się pobrać formularza komentarza ze strony wpisu."
            );
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var formActionMatch = CommentFormActionRegex.Match(html);
        if (!formActionMatch.Success)
        {
            return new CommentFormContext(
                false,
                null,
                null,
                "Komentowanie nie jest dostępne dla tego wpisu."
            );
        }

        var formActionUrl = Uri.TryCreate(
            WebUtility.HtmlDecode(formActionMatch.Groups["action"].Value),
            UriKind.Absolute,
            out var absoluteActionUrl
        )
            ? absoluteActionUrl
            : new Uri(postLink, WebUtility.HtmlDecode(formActionMatch.Groups["action"].Value));

        var akismetNonceMatch = AkismetNonceRegex.Match(html);
        var akismetNonce = akismetNonceMatch.Success
            ? WebUtility.HtmlDecode(akismetNonceMatch.Groups["value"].Value)
            : null;

        return new CommentFormContext(true, formActionUrl, akismetNonce, null);
    }

    private IEnumerable<KeyValuePair<string, string>> BuildLegacyCommentFields(
        WordPressCommentSubmissionRequest request,
        string? akismetNonce
    )
    {
        yield return new KeyValuePair<string, string>("comment", request.Content.Trim());
        yield return new KeyValuePair<string, string>("author", request.AuthorName.Trim());
        yield return new KeyValuePair<string, string>("email", request.AuthorEmail.Trim());
        yield return new KeyValuePair<string, string>("url", string.Empty);
        yield return new KeyValuePair<string, string>(
            "comment_post_ID",
            request.PostId.ToString()
        );
        yield return new KeyValuePair<string, string>(
            "comment_parent",
            request.ParentId.ToString()
        );
        yield return new KeyValuePair<string, string>("submit", "Dodaj komentarz");
        yield return new KeyValuePair<string, string>(
            "ak_js",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
        );
        yield return new KeyValuePair<string, string>("ak_hp_textarea", string.Empty);

        if (!string.IsNullOrWhiteSpace(akismetNonce))
        {
            yield return new KeyValuePair<string, string>("akismet_comment_nonce", akismetNonce);
        }
    }

    private async Task<WordPressCommentSubmissionResult> MapLegacySubmissionResponseAsync(
        int postId,
        Uri postLink,
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (IsRedirectStatusCode(response.StatusCode))
        {
            await InvalidateCommentsCacheAsync(postId, cancellationToken);

            var location = response.Headers.Location is null
                ? postLink
                : response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(postLink, response.Headers.Location);

            if (
                QueryContains(location, "unapproved")
                || QueryContains(location, "moderation-hash")
            )
            {
                return new WordPressCommentSubmissionResult(
                    true,
                    WordPressCommentSubmissionOutcome.PendingModeration,
                    "Komentarz został przekazany do moderacji."
                );
            }

            return new WordPressCommentSubmissionResult(
                true,
                WordPressCommentSubmissionOutcome.Published,
                "Komentarz został opublikowany."
            );
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ExtractLegacyErrorMessage(html);

        if (ContainsSpamSignal(message))
        {
            return new WordPressCommentSubmissionResult(
                true,
                WordPressCommentSubmissionOutcome.Spam,
                "Komentarz został zakwalifikowany jako spam."
            );
        }

        if (ContainsModerationSignal(message))
        {
            return new WordPressCommentSubmissionResult(
                true,
                WordPressCommentSubmissionOutcome.PendingModeration,
                "Komentarz został przekazany do moderacji."
            );
        }

        return new WordPressCommentSubmissionResult(
            false,
            WordPressCommentSubmissionOutcome.Rejected,
            string.IsNullOrWhiteSpace(message)
                ? "Nie udało się wysłać komentarza. Spróbuj ponownie później."
                : message
        );
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    }

    private static bool QueryContains(Uri uri, string key)
    {
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return false;
        }

        return uri.Query.Contains($"{key}=", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractLegacyErrorMessage(string html)
    {
        var match = ErrorMessageRegex.Match(html);
        if (!match.Success)
        {
            return null;
        }

        var withoutTags = HtmlTagRegex.Replace(match.Groups["message"].Value, " ");
        return WebUtility.HtmlDecode(withoutTags)
            .Replace('\u00A0', ' ')
            .Trim();
    }

    private static bool ContainsSpamSignal(string? message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && message.Contains("spam", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsModerationSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("moderac", StringComparison.OrdinalIgnoreCase)
            || message.Contains("oczekuje", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unapproved", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PostLinkPayload(string? Link);

    private sealed record CommentFormContext(
        bool CanSubmit,
        Uri? FormActionUrl,
        string? AkismetNonce,
        string? ErrorMessage
    );

    private static HttpRequestMessage CreateHtmlRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
        return request;
    }
}
