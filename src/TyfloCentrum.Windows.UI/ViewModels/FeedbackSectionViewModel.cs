using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class FeedbackSectionViewModel : ObservableObject
{
    private const string FallbackErrorMessage =
        "Nie udało się wysłać zgłoszenia. Spróbuj ponownie później.";
    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFeedbackSubmissionService _feedbackSubmissionService;

    public FeedbackSectionViewModel(
        IFeedbackSubmissionService feedbackSubmissionService,
        IExternalLinkLauncher externalLinkLauncher
    )
    {
        _feedbackSubmissionService = feedbackSubmissionService;
        _externalLinkLauncher = externalLinkLauncher;
        KindOptions = new[]
        {
            new FeedbackKindOptionViewModel(FeedbackSubmissionKind.Bug, "Błąd"),
            new FeedbackKindOptionViewModel(FeedbackSubmissionKind.Suggestion, "Sugestia"),
        };
        selectedKindOption = KindOptions[0];
    }

    public IReadOnlyList<FeedbackKindOptionViewModel> KindOptions { get; }

    [ObservableProperty]
    private FeedbackKindOptionViewModel? selectedKindOption;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string contactEmail = string.Empty;

    [ObservableProperty]
    private bool allowPrivateContactByEmail;

    [ObservableProperty]
    private bool includeDiagnostics = true;

    [ObservableProperty]
    private bool includeLogFile;

    [ObservableProperty]
    private bool isSubmitting;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? publicIssueUrl;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasPublicIssueUrl => !string.IsNullOrWhiteSpace(PublicIssueUrl);

    public bool CanSubmit =>
        !IsSubmitting
        && SelectedKindOption is not null
        && !string.IsNullOrWhiteSpace(Title.Trim())
        && !string.IsNullOrWhiteSpace(Description.Trim())
        && CanSubmitWithOptionalEmail();

    public bool CanOpenPublicIssue => !IsSubmitting && HasPublicIssueUrl;

    public string SubmitButtonText => IsSubmitting ? "Wysyłanie…" : "Wyślij zgłoszenie";

    public async Task<bool> SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSubmit)
        {
            if (!CanSubmitWithOptionalEmail())
            {
                ErrorMessage = GetContactEmailValidationMessage();
                StatusMessage = ErrorMessage;
                NotifyStateChanged();
            }

            return false;
        }

        IsSubmitting = true;
        ErrorMessage = null;
        StatusMessage = "Wysyłanie zgłoszenia…";
        NotifyStateChanged();

        try
        {
            var result = await _feedbackSubmissionService.SubmitAsync(
                new FeedbackSubmissionRequest(
                    SelectedKindOption!.Kind,
                    Title.Trim(),
                    Description.Trim(),
                    NormalizeOptionalEmail(ContactEmail),
                    IncludeDiagnostics,
                    IncludeLogFile
                ),
                cancellationToken
            );

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? FallbackErrorMessage;
                StatusMessage = ErrorMessage;
                NotifyStateChanged();
                return false;
            }

            PublicIssueUrl = result.PublicIssueUrl;
            Title = string.Empty;
            Description = string.Empty;
            ContactEmail = string.Empty;
            AllowPrivateContactByEmail = false;
            ErrorMessage = null;
            StatusMessage = HasPublicIssueUrl
                ? "Zgłoszenie wysłane. Możesz otworzyć publiczne issue w przeglądarce."
                : "Zgłoszenie wysłane pomyślnie.";
            NotifyStateChanged();
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
            IsSubmitting = false;
            NotifyStateChanged();
        }
    }

    public async Task<bool> OpenPublicIssueAsync(CancellationToken cancellationToken = default)
    {
        if (!HasPublicIssueUrl)
        {
            return false;
        }

        ErrorMessage = null;
        NotifyStateChanged();

        var launched = await _externalLinkLauncher.LaunchAsync(PublicIssueUrl!, cancellationToken);
        if (launched)
        {
            StatusMessage = "Otwarto publiczne zgłoszenie w przeglądarce.";
            NotifyStateChanged();
            return true;
        }

        ErrorMessage = "Nie udało się otworzyć publicznego zgłoszenia w przeglądarce.";
        StatusMessage = ErrorMessage;
        NotifyStateChanged();
        return false;
    }

    partial void OnSelectedKindOptionChanged(FeedbackKindOptionViewModel? value)
    {
        ErrorMessage = null;
        if (!string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Description))
        {
            ClearPreviousSubmissionState();
        }

        NotifyStateChanged();
    }

    partial void OnTitleChanged(string value)
    {
        ErrorMessage = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            ClearPreviousSubmissionState();
        }

        NotifyStateChanged();
    }

    partial void OnDescriptionChanged(string value)
    {
        ErrorMessage = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            ClearPreviousSubmissionState();
        }

        NotifyStateChanged();
    }

    partial void OnContactEmailChanged(string value)
    {
        ErrorMessage = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            ClearPreviousSubmissionState();
        }
        else
        {
            AllowPrivateContactByEmail = false;
        }

        NotifyStateChanged();
    }

    partial void OnAllowPrivateContactByEmailChanged(bool value)
    {
        ErrorMessage = null;
        NotifyStateChanged();
    }

    partial void OnIncludeDiagnosticsChanged(bool value)
    {
        NotifyStateChanged();
    }

    partial void OnIncludeLogFileChanged(bool value)
    {
        NotifyStateChanged();
    }

    partial void OnIsSubmittingChanged(bool value)
    {
        NotifyStateChanged();
    }

    partial void OnPublicIssueUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPublicIssueUrl));
        OnPropertyChanged(nameof(CanOpenPublicIssue));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanOpenPublicIssue));
        OnPropertyChanged(nameof(SubmitButtonText));
    }

    private void ClearPreviousSubmissionState()
    {
        StatusMessage = null;
        PublicIssueUrl = null;
    }

    private bool CanSubmitWithOptionalEmail()
    {
        var normalizedEmail = NormalizeOptionalEmail(ContactEmail);
        if (normalizedEmail is null)
        {
            return true;
        }

        return AllowPrivateContactByEmail && EmailValidator.IsValid(normalizedEmail);
    }

    private string? GetContactEmailValidationMessage()
    {
        var normalizedEmail = NormalizeOptionalEmail(ContactEmail);
        if (normalizedEmail is null)
        {
            return null;
        }

        if (!AllowPrivateContactByEmail)
        {
            return "Aby wysłać adres e-mail do prywatnego repo diagnostycznego, zaznacz zgodę poniżej.";
        }

        return EmailValidator.IsValid(normalizedEmail)
            ? null
            : "Wpisany adres e-mail ma nieprawidłowy format.";
    }

    private static string? NormalizeOptionalEmail(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
