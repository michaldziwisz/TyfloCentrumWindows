using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class ContactVoiceMessageViewModel : ObservableObject
{
    private const string NameKey = "contact.radio.name";
    private const string FallbackErrorMessage = "Nie udało się wysłać głosówki. Spróbuj ponownie.";

    private readonly ILocalSettingsStore _localSettingsStore;
    private readonly IRadioVoiceContactService _radioVoiceContactService;
    private readonly IVoiceMessageRecorder _voiceMessageRecorder;
    private bool _hasLoadedDraft;
    private bool _isRestoringDraft;
    private bool _isStoppingRecording;
    private RecordingMode _recordingMode = RecordingMode.None;

    public ContactVoiceMessageViewModel(
        IRadioVoiceContactService radioVoiceContactService,
        IVoiceMessageRecorder voiceMessageRecorder,
        ILocalSettingsStore localSettingsStore
    )
    {
        _radioVoiceContactService = radioVoiceContactService;
        _voiceMessageRecorder = voiceMessageRecorder;
        _localSettingsStore = localSettingsStore;
        _voiceMessageRecorder.PropertyChanged += OnVoiceMessageRecorderPropertyChanged;
        _voiceMessageRecorder.NotificationRaised += OnVoiceMessageRecorderNotificationRaised;
    }

    public event EventHandler? MessageSent;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isSending;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsRecording => _voiceMessageRecorder.State == VoiceMessageRecorderState.Recording;

    public bool IsPreparing => _voiceMessageRecorder.State == VoiceMessageRecorderState.Preparing;

    public bool HasRecording => _voiceMessageRecorder.HasRecording;

    public bool IsPlayingPreview =>
        _voiceMessageRecorder.State == VoiceMessageRecorderState.PlayingPreview;

    public string DurationText => FormatDuration(_voiceMessageRecorder.Elapsed);

    public int SegmentCount => _voiceMessageRecorder.SegmentCount;

    public string SegmentCountText => FormatSegmentCount(SegmentCount);

    public string? RecordedFilePath => _voiceMessageRecorder.RecordedFilePath;

    public string StartButtonText => HasRecording ? "Nagraj od nowa" : "Rozpocznij nagrywanie";

    public string AppendButtonText => "Dograj fragment";

    public string PreviewButtonText => IsPlayingPreview ? "Zatrzymaj odsłuch" : "Odsłuchaj";

    public string HoldToTalkButtonText =>
        IsRecording ? "Puść, aby zakończyć" : HasRecording ? "Przytrzymaj i dograj" : "Przytrzymaj i mów";

    public bool CanStartRecording =>
        !IsSending && !IsPreparing && !IsRecording && !string.IsNullOrWhiteSpace(Name.Trim());

    public bool CanAppendRecording =>
        !IsSending
        && !IsPreparing
        && HasRecording
        && !IsRecording
        && !string.IsNullOrWhiteSpace(Name.Trim());

    public bool CanStopRecording => !IsSending && !_isStoppingRecording && IsRecording;

    public bool CanTogglePreview => !IsSending && !IsPreparing && HasRecording && !IsRecording;

    public bool CanDeleteRecording => !IsSending && !IsPreparing && HasRecording && !IsRecording;

    public bool CanSend =>
        !IsSending
        && !IsPreparing
        && HasRecording
        && !IsRecording
        && !string.IsNullOrWhiteSpace(Name.Trim())
        && _voiceMessageRecorder.RecordedDurationMs > 0
        && !string.IsNullOrWhiteSpace(_voiceMessageRecorder.RecordedFilePath);

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasLoadedDraft)
        {
            return;
        }

        _hasLoadedDraft = true;
        _isRestoringDraft = true;

        try
        {
            Name = await _localSettingsStore.GetStringAsync(NameKey, cancellationToken) ?? string.Empty;
        }
        finally
        {
            _isRestoringDraft = false;
            NotifyStateChanged();
        }
    }

    public async Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        await StartRecordingCoreAsync(cueAlreadyAnnounced: false, cancellationToken);
    }

    public async Task<VoiceMessageOperationResult> EnsureMicrophoneAccessAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _voiceMessageRecorder.EnsureMicrophoneAccessAsync(cancellationToken);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            StatusMessage = result.ErrorMessage;
            NotifyStateChanged();
        }

        return result;
    }

    public async Task StartRecordingAfterCueAsync(CancellationToken cancellationToken = default)
    {
        await StartRecordingCoreAsync(cueAlreadyAnnounced: true, cancellationToken);
    }

    public async Task StartAppendingAsync(CancellationToken cancellationToken = default)
    {
        await StartAppendingCoreAsync(cueAlreadyAnnounced: false, cancellationToken);
    }

    public async Task StartAppendingAfterCueAsync(CancellationToken cancellationToken = default)
    {
        await StartAppendingCoreAsync(cueAlreadyAnnounced: true, cancellationToken);
    }

    public void AnnounceRecordingStartCue(bool append)
    {
        ErrorMessage = null;
        StatusMessage = append ? "Dograj fragment po sygnale." : "Nagraj wiadomość po sygnale.";
        NotifyStateChanged();
    }

    public void AnnounceMicrophoneAccessCheck(bool append)
    {
        ErrorMessage = null;
        StatusMessage = append
            ? "Sprawdzanie dostępu do mikrofonu przed dograniem fragmentu. Windows może teraz poprosić o zgodę na użycie mikrofonu."
            : "Sprawdzanie dostępu do mikrofonu. Windows może teraz poprosić o zgodę na użycie mikrofonu.";
        NotifyStateChanged();
    }

    private async Task StartRecordingCoreAsync(
        bool cueAlreadyAnnounced,
        CancellationToken cancellationToken
    )
    {
        if (!CanStartRecording)
        {
            ErrorMessage = "Uzupełnij imię, aby nagrać głosówkę.";
            NotifyStateChanged();
            return;
        }

        ErrorMessage = null;
        StatusMessage = cueAlreadyAnnounced
            ? null
            : HasRecording
                ? "Uruchamianie nowego nagrania. Poprzednia wersja zostanie zastąpiona."
                : "Uruchamianie nagrywania. Windows może teraz poprosić o zgodę na użycie mikrofonu.";
        _recordingMode = RecordingMode.New;
        NotifyStateChanged();

        var result = await _voiceMessageRecorder.StartRecordingAsync(cancellationToken);
        if (!result.Success)
        {
            _recordingMode = RecordingMode.None;
            ErrorMessage = result.ErrorMessage;
            StatusMessage = result.ErrorMessage;
        }
        else
        {
            StatusMessage = null;
        }

        NotifyStateChanged();
    }

    private async Task StartAppendingCoreAsync(
        bool cueAlreadyAnnounced,
        CancellationToken cancellationToken
    )
    {
        if (!CanAppendRecording)
        {
            ErrorMessage = HasRecording
                ? "Uzupełnij imię, aby dograć kolejny fragment."
                : "Najpierw nagraj pierwszy fragment.";
            NotifyStateChanged();
            return;
        }

        ErrorMessage = null;
        StatusMessage = cueAlreadyAnnounced
            ? null
            : "Uruchamianie surowego nagrywania kolejnego fragmentu…";
        _recordingMode = RecordingMode.Append;
        NotifyStateChanged();

        var result = await _voiceMessageRecorder.StartAppendingAsync(cancellationToken);
        if (!result.Success)
        {
            _recordingMode = RecordingMode.None;
            ErrorMessage = result.ErrorMessage;
            StatusMessage = result.ErrorMessage;
        }
        else
        {
            StatusMessage = null;
        }

        NotifyStateChanged();
    }

    public Task StartHoldToTalkAsync(CancellationToken cancellationToken = default)
    {
        return HasRecording
            ? StartAppendingAsync(cancellationToken)
            : StartRecordingAsync(cancellationToken);
    }

    public Task StopHoldToTalkAsync(CancellationToken cancellationToken = default)
    {
        return StopRecordingAsync(cancellationToken);
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording || _isStoppingRecording)
        {
            return;
        }

        _isStoppingRecording = true;
        var wasAppend = _recordingMode == RecordingMode.Append;
        StatusMessage = wasAppend
            ? "Dodawanie fragmentu. Przygotowywanie nagrania…"
            : "Zatrzymywanie nagrania…";
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var result = await _voiceMessageRecorder.StopRecordingAsync(cancellationToken);
            if (!result.Success)
            {
                _recordingMode = RecordingMode.None;
                ErrorMessage = result.ErrorMessage;
                StatusMessage = result.ErrorMessage;
            }
            else
            {
                ErrorMessage = null;
                StatusMessage = wasAppend
                    ? "Fragment dodany. Nagranie gotowe do odsłuchu lub wysłania."
                    : "Nagranie zapisane. Możesz je odsłuchać lub wysłać.";
                _recordingMode = RecordingMode.None;
            }
        }
        finally
        {
            _isStoppingRecording = false;
            NotifyStateChanged();
        }
    }

    public async Task TogglePreviewAsync(CancellationToken cancellationToken = default)
    {
        var wasPlayingPreview = IsPlayingPreview;
        var result = await _voiceMessageRecorder.TogglePreviewAsync(cancellationToken);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            StatusMessage = result.ErrorMessage;
        }
        else
        {
            ErrorMessage = null;
            StatusMessage = wasPlayingPreview
                ? "Odtwarzanie podglądu zakończone."
                : "Odtwarzanie nagrania.";
        }

        NotifyStateChanged();
    }

    public async Task FinishPreviewAsync(CancellationToken cancellationToken = default)
    {
        await _voiceMessageRecorder.StopPreviewAsync(cancellationToken);
        ErrorMessage = null;
        StatusMessage = "Odtwarzanie podglądu zakończone.";
        NotifyStateChanged();
    }

    public async Task FailPreviewAsync(CancellationToken cancellationToken = default)
    {
        await _voiceMessageRecorder.StopPreviewAsync(cancellationToken);
        ErrorMessage = "Nie udało się odtworzyć nagrania.";
        StatusMessage = ErrorMessage;
        NotifyStateChanged();
    }

    public async Task DeleteRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _voiceMessageRecorder.DeleteRecordingAsync(cancellationToken);
        _recordingMode = RecordingMode.None;
        ErrorMessage = null;
        StatusMessage = "Nagranie usunięte.";
        NotifyStateChanged();
    }

    public async Task<bool> SendAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSend)
        {
            return false;
        }

        if (IsPlayingPreview)
        {
            await _voiceMessageRecorder.TogglePreviewAsync(cancellationToken);
        }

        IsSending = true;
        ErrorMessage = null;
        StatusMessage = "Wysyłanie głosówki…";
        NotifyStateChanged();

        try
        {
            var result = await _radioVoiceContactService.SendVoiceMessageAsync(
                Name.Trim(),
                _voiceMessageRecorder.RecordedFilePath!,
                _voiceMessageRecorder.RecordedDurationMs,
                cancellationToken
            );

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? FallbackErrorMessage;
                StatusMessage = ErrorMessage;
                NotifyStateChanged();
                return false;
            }

            await _voiceMessageRecorder.DeleteRecordingAsync(cancellationToken);
            ErrorMessage = null;
            StatusMessage = "Głosówka wysłana pomyślnie.";
            NotifyStateChanged();
            MessageSent?.Invoke(this, EventArgs.Empty);
            _recordingMode = RecordingMode.None;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ErrorMessage = FallbackErrorMessage;
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return false;
        }
        finally
        {
            IsSending = false;
            NotifyStateChanged();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _voiceMessageRecorder.DeleteRecordingAsync(cancellationToken);
        _recordingMode = RecordingMode.None;
        ErrorMessage = null;
        StatusMessage = null;
        NotifyStateChanged();
    }

    partial void OnNameChanged(string value)
    {
        if (!_isRestoringDraft)
        {
            _ = PersistNameAsync(value);
        }

        ErrorMessage = null;
        NotifyStateChanged();
    }

    partial void OnIsSendingChanged(bool value)
    {
        NotifyStateChanged();
    }

    private void OnVoiceMessageRecorderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyStateChanged();
    }

    private void OnVoiceMessageRecorderNotificationRaised(
        object? sender,
        VoiceMessageRecorderNotificationEventArgs e
    )
    {
        _recordingMode = RecordingMode.None;
        ErrorMessage = e.IsError ? e.Message : null;
        StatusMessage = e.Message;
        NotifyStateChanged();
    }

    private async Task PersistNameAsync(string value)
    {
        try
        {
            await _localSettingsStore.SetStringAsync(NameKey, value);
        }
        catch
        {
            // Best effort only.
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPreparing));
        OnPropertyChanged(nameof(HasRecording));
        OnPropertyChanged(nameof(IsPlayingPreview));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(SegmentCount));
        OnPropertyChanged(nameof(SegmentCountText));
        OnPropertyChanged(nameof(RecordedFilePath));
        OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(AppendButtonText));
        OnPropertyChanged(nameof(PreviewButtonText));
        OnPropertyChanged(nameof(HoldToTalkButtonText));
        OnPropertyChanged(nameof(CanStartRecording));
        OnPropertyChanged(nameof(CanAppendRecording));
        OnPropertyChanged(nameof(CanStopRecording));
        OnPropertyChanged(nameof(CanTogglePreview));
        OnPropertyChanged(nameof(CanDeleteRecording));
        OnPropertyChanged(nameof(CanSend));
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");
    }

    private static string FormatSegmentCount(int value)
    {
        return value switch
        {
            1 => "1 fragment",
            >= 2 and <= 4 => $"{value} fragmenty",
            _ => $"{value} fragmentów",
        };
    }

    private enum RecordingMode
    {
        None,
        New,
        Append,
    }
}
