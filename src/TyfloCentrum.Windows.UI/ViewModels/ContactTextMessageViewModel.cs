using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class ContactTextMessageViewModel : ObservableObject
{
    private const string DefaultMessage = "\nWysłane przy pomocy aplikacji TyfloCentrum";
    private const string NameKey = "contact.radio.name";
    private const string MessageKey = "contact.radio.message";
    private const string FallbackErrorMessage = "Nie udało się wysłać wiadomości. Spróbuj ponownie.";

    private readonly ILocalSettingsStore _localSettingsStore;
    private readonly IRadioContactService _radioContactService;
    private bool _hasLoadedDraft;
    private bool _isRestoringDraft;

    public ContactTextMessageViewModel(
        IRadioContactService radioContactService,
        ILocalSettingsStore localSettingsStore
    )
    {
        _radioContactService = radioContactService;
        _localSettingsStore = localSettingsStore;
        message = DefaultMessage;
    }

    public event EventHandler? MessageSent;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string message;

    [ObservableProperty]
    private bool isSending;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool CanSend =>
        !IsSending
        && !string.IsNullOrWhiteSpace(Name.Trim())
        && !string.IsNullOrWhiteSpace(Message.Trim());

    public string SendButtonText => IsSending ? "Wysyłanie…" : "Wyślij wiadomość";

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
            var storedMessage = await _localSettingsStore.GetStringAsync(MessageKey, cancellationToken);
            Message = string.IsNullOrWhiteSpace(storedMessage) ? DefaultMessage : storedMessage;
        }
        finally
        {
            _isRestoringDraft = false;
            NotifyStateChanged();
        }
    }

    public async Task<bool> SendAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSend)
        {
            return false;
        }

        IsSending = true;
        ErrorMessage = null;
        StatusMessage = "Wysyłanie wiadomości…";
        NotifyStateChanged();

        try
        {
            var result = await _radioContactService.SendMessageAsync(
                Name.Trim(),
                Message,
                cancellationToken
            );

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? FallbackErrorMessage;
                StatusMessage = ErrorMessage;
                NotifyStateChanged();
                return false;
            }

            Message = DefaultMessage;
            ErrorMessage = null;
            StatusMessage = "Wiadomość wysłana pomyślnie.";
            NotifyStateChanged();
            MessageSent?.Invoke(this, EventArgs.Empty);
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

    partial void OnNameChanged(string value)
    {
        PersistDraft(NameKey, value);
        ErrorMessage = null;
        NotifyStateChanged();
    }

    partial void OnMessageChanged(string value)
    {
        PersistDraft(MessageKey, value);
        ErrorMessage = null;
        NotifyStateChanged();
    }

    partial void OnIsSendingChanged(bool value)
    {
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(SendButtonText));
    }

    private void PersistDraft(string key, string value)
    {
        if (_isRestoringDraft)
        {
            return;
        }

        _ = PersistDraftCoreAsync(key, value);
    }

    private async Task PersistDraftCoreAsync(string key, string value)
    {
        try
        {
            await _localSettingsStore.SetStringAsync(key, value);
        }
        catch
        {
            // Best effort only. Draft persistence must not block the form.
        }
    }
}
