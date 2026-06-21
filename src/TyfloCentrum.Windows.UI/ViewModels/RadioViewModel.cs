using CommunityToolkit.Mvvm.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class RadioViewModel : ObservableObject
{
    private const string ContactUnavailableMessage =
        "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna, więc nie można wysłać wiadomości tekstowej.";
    private const string VoiceContactUnavailableMessage =
        "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna, więc nie można nagrać głosówki.";
    private const string ContactDialogOpenErrorMessage =
        "Nie udało się otworzyć formularza wiadomości do Tyfloradia.";
    private const string VoiceContactDialogOpenErrorMessage =
        "Nie udało się otworzyć formularza głosówki do Tyfloradia.";
    private readonly IAudioPlaybackRequestFactory _audioPlaybackRequestFactory;
    private readonly IRadioService _radioService;
    private bool _hasRequestedInitialLoad;

    public RadioViewModel(
        IRadioService radioService,
        IAudioPlaybackRequestFactory audioPlaybackRequestFactory
    )
    {
        _radioService = radioService;
        _audioPlaybackRequestFactory = audioPlaybackRequestFactory;
    }

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? liveStatusMessage;

    [ObservableProperty]
    private string? scheduleText;

    [ObservableProperty]
    private string scheduleHtmlDocument = BuildScheduleHtmlDocument(null, "Ładowanie ramówki…");

    [ObservableProperty]
    private string? statusAnnouncement;

    [ObservableProperty]
    private bool isInteractiveBroadcastAvailable;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasScheduleText => !string.IsNullOrWhiteSpace(ScheduleText);

    public bool CanOpenContact => !IsLoading;

    public bool CanOpenSchedule => !IsLoading && (HasLoaded || HasError);

    public string LiveStatusDisplayText =>
        !string.IsNullOrWhiteSpace(LiveStatusMessage)
            ? LiveStatusMessage
            : (IsLoading ? "Ładowanie bieżącego statusu Tyfloradia…" : string.Empty);

    public string LiveStatusAccessibleText =>
        string.IsNullOrWhiteSpace(LiveStatusDisplayText)
            ? "Status audycji interaktywnej"
            : $"Status audycji interaktywnej. {LiveStatusDisplayText}";

    public string ScheduleDisplayText =>
        HasScheduleText
            ? ScheduleText!
            : (IsLoading && !HasLoaded
                ? "Ładowanie ramówki…"
                : (!string.IsNullOrWhiteSpace(ErrorMessage) ? ErrorMessage : ScheduleFallbackMessage));

    public string ScheduleAccessibleText =>
        string.IsNullOrWhiteSpace(ScheduleDisplayText)
            ? "Ramówka"
            : $"Ramówka. {ScheduleDisplayText}";

    public string ScheduleFallbackMessage =>
        HasLoaded && !HasScheduleText ? "Brak dostępnej ramówki." : string.Empty;

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasRequestedInitialLoad)
        {
            return;
        }

        _hasRequestedInitialLoad = true;
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusAnnouncement = "Ładowanie danych Tyfloradia…";
        NotifyStateChanged();

        try
        {
            var availabilityTask = _radioService.GetAvailabilityAsync(cancellationToken);
            var scheduleTask = _radioService.GetScheduleAsync(cancellationToken);

            await Task.WhenAll(availabilityTask, scheduleTask);

            var availability = availabilityTask.Result;
            var schedule = scheduleTask.Result;

            IsInteractiveBroadcastAvailable = availability.Available;
            LiveStatusMessage = BuildLiveStatusMessage(availability.Available, availability.Title);
            ScheduleText = NormalizeScheduleText(schedule.Text);
            ErrorMessage = schedule.Error;
            ScheduleHtmlDocument = BuildScheduleHtmlDocument(
                schedule.Text,
                BuildScheduleFallbackDisplayText(ScheduleText, ErrorMessage)
            );
            StatusAnnouncement = ErrorMessage
                ?? (availability.Available
                    ? "Dane Tyfloradia zostały odświeżone."
                    : "Tyfloradio nie prowadzi teraz audycji interaktywnej.");
            HasLoaded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            IsInteractiveBroadcastAvailable = false;
            LiveStatusMessage = "Nie udało się pobrać bieżącego statusu Tyfloradia.";
            ScheduleText = null;
            ErrorMessage = "Nie udało się pobrać danych Tyfloradia. Spróbuj ponownie.";
            ScheduleHtmlDocument = BuildScheduleHtmlDocument(null, ErrorMessage);
            StatusAnnouncement = ErrorMessage;
            HasLoaded = true;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public AudioPlaybackRequest CreatePlaybackRequest()
    {
        return _audioPlaybackRequestFactory.CreateRadio(
            LiveStatusMessage,
            IsInteractiveBroadcastAvailable
        );
    }

    public void ReportPlaybackError()
    {
        ErrorMessage = "Nie udało się uruchomić odtwarzacza Tyfloradia.";
        StatusAnnouncement = ErrorMessage;
        NotifyStateChanged();
    }

    public bool TryStartContact()
    {
        return TryStartTextContact();
    }

    public bool TryStartTextContact()
    {
        return TryStartContactCore(ContactUnavailableMessage);
    }

    public bool TryStartVoiceContact()
    {
        return TryStartContactCore(VoiceContactUnavailableMessage);
    }

    public void ReportContactFormOpenError()
    {
        SetFeedback(null, ContactDialogOpenErrorMessage, forceAnnouncement: true);
    }

    public void ReportVoiceContactFormOpenError()
    {
        SetFeedback(null, VoiceContactDialogOpenErrorMessage, forceAnnouncement: true);
    }

    private bool TryStartContactCore(string unavailableMessage)
    {
        if (!IsInteractiveBroadcastAvailable)
        {
            SetFeedback(null, unavailableMessage, forceAnnouncement: true);
            return false;
        }

        return true;
    }

    public void ReportContactSent()
    {
        SetFeedback(null, "Wiadomość wysłana pomyślnie.");
    }

    public void ReportVoiceMessageSent()
    {
        SetFeedback(null, "Głosówka wysłana pomyślnie.");
    }

    private void SetFeedback(
        string? errorMessage,
        string? statusAnnouncement,
        bool forceAnnouncement = false
    )
    {
        if (
            forceAnnouncement
            && string.Equals(ErrorMessage, errorMessage, StringComparison.Ordinal)
            && string.Equals(StatusAnnouncement, statusAnnouncement, StringComparison.Ordinal)
        )
        {
            ErrorMessage = null;
            StatusAnnouncement = null;
        }

        ErrorMessage = errorMessage;
        StatusAnnouncement = statusAnnouncement;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasScheduleText));
        OnPropertyChanged(nameof(CanOpenContact));
        OnPropertyChanged(nameof(CanOpenSchedule));
        OnPropertyChanged(nameof(LiveStatusDisplayText));
        OnPropertyChanged(nameof(LiveStatusAccessibleText));
        OnPropertyChanged(nameof(ScheduleDisplayText));
        OnPropertyChanged(nameof(ScheduleAccessibleText));
        OnPropertyChanged(nameof(ScheduleFallbackMessage));
    }

    private static string BuildLiveStatusMessage(bool isAvailable, string? title)
    {
        if (!isAvailable)
        {
            return "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna.";
        }

        return string.IsNullOrWhiteSpace(title)
            ? "Na antenie trwa audycja interaktywna."
            : $"Na antenie trwa audycja interaktywna: {title}.";
    }

    private static string? NormalizeScheduleText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        normalized = normalized.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\r", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\\r\\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\\r", "\n", StringComparison.Ordinal);
        normalized = ScheduleBreakRegex().Replace(normalized, "\n");
        normalized = ScheduleParagraphRegex().Replace(normalized, "\n");
        normalized = ScheduleTagRegex().Replace(normalized, string.Empty);
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = normalized.Replace("\u00A0", " ", StringComparison.Ordinal);
        normalized = MultiBlankLineRegex().Replace(normalized, "\n\n");

        return normalized.Trim();
    }

    private static string BuildScheduleFallbackDisplayText(string? scheduleText, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(scheduleText))
        {
            return scheduleText;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return errorMessage;
        }

        return "Brak dostępnej ramówki.";
    }

    private static string BuildScheduleHtmlDocument(string? rawSchedule, string fallbackText)
    {
        var body = BuildScheduleHtmlBody(rawSchedule, fallbackText);
        return $$"""
            <!doctype html>
            <html lang="pl">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Ramówka Tyfloradia</title>
              <style>
                :root { color-scheme: light dark; }
                body {
                  font-family: "Segoe UI", sans-serif;
                  font-size: 18px;
                  line-height: 1.55;
                  margin: 24px;
                }
                main {
                  max-width: 48rem;
                }
                h1 {
                  font-size: 1.45rem;
                  margin: 0 0 1rem;
                }
                a {
                  color: LinkText;
                }
                a:focus {
                  outline: 3px solid Highlight;
                  outline-offset: 2px;
                }
                main:focus {
                  outline: 3px solid Highlight;
                  outline-offset: 4px;
                }
                p, ul, ol, table {
                  margin-block: 0 1rem;
                }
                table {
                  border-collapse: collapse;
                }
                th, td {
                  border: 1px solid CanvasText;
                  padding: 0.35rem 0.5rem;
                }
              </style>
            </head>
            <body>
              <main id="schedule-root" tabindex="0">
                <h1>Ramówka Tyfloradia</h1>
                {{body}}
              </main>
              <script>
                (function () {
                  const webview = window.chrome && window.chrome.webview;
                  const root = document.getElementById('schedule-root');
                  if (root) { setTimeout(() => root.focus(), 0); }
                  document.addEventListener('keydown', event => {
                    if (event.key === 'Escape' && webview) {
                      event.preventDefault();
                      webview.postMessage('closeSchedule');
                    }
                  });
                  document.addEventListener('click', event => {
                    const anchor = event.target.closest('a[href]');
                    if (!anchor || !webview) { return; }
                    const href = anchor.href || anchor.getAttribute('href');
                    if (!href || href === '#') { return; }
                    event.preventDefault();
                    webview.postMessage('openExternal:' + href);
                  });
                }());
              </script>
            </body>
            </html>
            """;
    }

    private static string BuildScheduleHtmlBody(string? rawSchedule, string fallbackText)
    {
        if (string.IsNullOrWhiteSpace(rawSchedule))
        {
            return PlainTextToHtml(fallbackText);
        }

        var normalized = NormalizeRawScheduleMarkup(rawSchedule.Trim());
        if (!LooksLikeHtml(normalized))
        {
            return PlainTextToHtml(WebUtility.HtmlDecode(normalized));
        }

        normalized = UnsafeScheduleElementRegex().Replace(normalized, string.Empty);
        normalized = EventAttributeRegex().Replace(normalized, string.Empty);
        normalized = JavaScriptUrlRegex().Replace(normalized, "href=\"#\"");
        return normalized;
    }

    private static string NormalizeRawScheduleMarkup(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\r", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\\r\\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace("\\r", "\n", StringComparison.Ordinal);
        return normalized;
    }

    private static string PlainTextToHtml(string value)
    {
        var normalized = NormalizeRawScheduleMarkup(value);
        var builder = new StringBuilder();
        var currentIndex = 0;

        foreach (Match match in PlainUrlRegex().Matches(normalized))
        {
            builder.Append(EncodeScheduleTextSegment(normalized[currentIndex..match.Index]));

            var linkText = match.Value;
            var linkTarget = TrimLinkTrailingPunctuation(linkText, out var trailingText);
            var encodedLink = WebUtility.HtmlEncode(linkTarget);
            builder.Append("<a href=\"");
            builder.Append(encodedLink);
            builder.Append("\">");
            builder.Append(encodedLink);
            builder.Append("</a>");
            builder.Append(EncodeScheduleTextSegment(trailingText));

            currentIndex = match.Index + match.Length;
        }

        builder.Append(EncodeScheduleTextSegment(normalized[currentIndex..]));
        return builder.ToString();
    }

    private static bool LooksLikeHtml(string value)
    {
        return HtmlTagProbeRegex().IsMatch(value);
    }

    private static string EncodeScheduleTextSegment(string value)
    {
        return WebUtility.HtmlEncode(value).Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string TrimLinkTrailingPunctuation(string value, out string trailingText)
    {
        var end = value.Length;
        while (end > 0 && IsTrailingLinkPunctuation(value[end - 1]))
        {
            end--;
        }

        trailingText = value[end..];
        return value[..end];
    }

    private static bool IsTrailingLinkPunctuation(char value)
    {
        return value is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']';
    }

    [GeneratedRegex(@"(?i)<br\s*/?>", RegexOptions.Compiled)]
    private static partial Regex ScheduleBreakRegex();

    [GeneratedRegex(@"(?i)</p>\s*<p[^>]*>", RegexOptions.Compiled)]
    private static partial Regex ScheduleParagraphRegex();

    [GeneratedRegex(@"(?i)<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex ScheduleTagRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultiBlankLineRegex();

    [GeneratedRegex(@"<\s*[a-z][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HtmlTagProbeRegex();

    [GeneratedRegex(
        @"(?is)<\s*(script|style|iframe|object|embed)[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.Compiled
    )]
    private static partial Regex UnsafeScheduleElementRegex();

    [GeneratedRegex(@"\s+on[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EventAttributeRegex();

    [GeneratedRegex(@"href\s*=\s*(""|')\s*javascript:[^""']*\1", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JavaScriptUrlRegex();

    [GeneratedRegex(@"(?:https?://|mailto:)[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlainUrlRegex();
}
