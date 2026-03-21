using CommunityToolkit.Mvvm.ComponentModel;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.UI.ViewModels;

public partial class RadioViewModel : ObservableObject
{
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
            ScheduleText = schedule.Text?.Trim();
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
        if (!IsInteractiveBroadcastAvailable)
        {
            ErrorMessage = "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna.";
            StatusAnnouncement = ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        ErrorMessage = null;
        NotifyStateChanged();
        return true;
    }

    public void ReportContactSent()
    {
        ErrorMessage = null;
        StatusAnnouncement = "Wiadomość wysłana pomyślnie.";
        NotifyStateChanged();
    }

    public void ReportVoiceMessageSent()
    {
        ErrorMessage = null;
        StatusAnnouncement = "Głosówka wysłana pomyślnie.";
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasScheduleText));
        OnPropertyChanged(nameof(CanOpenContact));
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
}
