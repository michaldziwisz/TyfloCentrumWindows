using CommunityToolkit.Mvvm.ComponentModel;
using System.Net;
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
        return _audioPlaybackRequestFactory.CreateRadio(LiveStatusMessage);
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

    [GeneratedRegex(@"(?i)<br\s*/?>", RegexOptions.Compiled)]
    private static partial Regex ScheduleBreakRegex();

    [GeneratedRegex(@"(?i)</p>\s*<p[^>]*>", RegexOptions.Compiled)]
    private static partial Regex ScheduleParagraphRegex();

    [GeneratedRegex(@"(?i)<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex ScheduleTagRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultiBlankLineRegex();
}
