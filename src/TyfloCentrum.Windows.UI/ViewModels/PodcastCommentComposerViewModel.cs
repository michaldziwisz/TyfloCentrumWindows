using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class PodcastCommentComposerViewModel : ObservableObject
{
    private const string AuthorNameKey = "comments.wordpress.authorName";
    private const string AuthorEmailKey = "comments.wordpress.authorEmail";
    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly ILocalSettingsStore _localSettingsStore;
    private readonly IWordPressCommentsService _wordPressCommentsService;
    private bool _hasLoadedDraft;
    private bool _isRestoringDraft;
    private int _postId;

    public PodcastCommentComposerViewModel(
        IWordPressCommentsService wordPressCommentsService,
        ILocalSettingsStore localSettingsStore
    )
    {
        _wordPressCommentsService = wordPressCommentsService;
        _localSettingsStore = localSettingsStore;
    }

    [ObservableProperty]
    private string authorName = string.Empty;

    [ObservableProperty]
    private string authorEmail = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private bool isSubmitting;

    [ObservableProperty]
    private int replyToCommentId;

    [ObservableProperty]
    private string? replyToAuthorName;

    public bool IsReplyMode => ReplyToCommentId > 0 && !string.IsNullOrWhiteSpace(ReplyToAuthorName);

    public bool HasReplyTarget => IsReplyMode;

    public bool CanSubmit => !IsSubmitting && _postId > 0;

    public string SubmitButtonText =>
        IsSubmitting
            ? "Wysyłanie…"
            : IsReplyMode
                ? "Wyślij odpowiedź"
                : "Wyślij komentarz";

    public string FormHeadingText => IsReplyMode ? "Odpowiedź na komentarz" : "Nowy komentarz";

    public string ReplyTargetText =>
        IsReplyMode ? $"Odpowiadasz na komentarz autora: {ReplyToAuthorName}" : string.Empty;

    public string ReplyTargetVisibility => HasReplyTarget ? "Visible" : "Collapsed";

    public void Initialize(int postId)
    {
        _postId = postId;
        NotifyStateChanged();
    }

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
            AuthorName =
                await _localSettingsStore.GetStringAsync(AuthorNameKey, cancellationToken) ?? string.Empty;
            AuthorEmail =
                await _localSettingsStore.GetStringAsync(AuthorEmailKey, cancellationToken) ?? string.Empty;
        }
        finally
        {
            _isRestoringDraft = false;
            NotifyStateChanged();
        }
    }

    public void BeginReply(CommentItemViewModel item)
    {
        ReplyToCommentId = item.Id;
        ReplyToAuthorName = item.AuthorName;
        NotifyStateChanged();
    }

    public void CancelReply()
    {
        ReplyToCommentId = 0;
        ReplyToAuthorName = null;
        NotifyStateChanged();
    }

    public async Task<PodcastCommentSubmitAttemptResult> SubmitAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (_postId <= 0)
        {
            return new PodcastCommentSubmitAttemptResult(
                false,
                "Nie udało się ustalić wpisu, do którego ma trafić komentarz."
            );
        }

        if (string.IsNullOrWhiteSpace(AuthorName))
        {
            return new PodcastCommentSubmitAttemptResult(
                false,
                "Pole Imię jest obowiązkowe.",
                PodcastCommentFormField.AuthorName
            );
        }

        if (string.IsNullOrWhiteSpace(AuthorEmail))
        {
            return new PodcastCommentSubmitAttemptResult(
                false,
                "Pole Adres e-mail jest obowiązkowe.",
                PodcastCommentFormField.AuthorEmail
            );
        }

        if (!EmailValidator.IsValid(AuthorEmail.Trim()))
        {
            return new PodcastCommentSubmitAttemptResult(
                false,
                "Wpisz poprawny adres e-mail.",
                PodcastCommentFormField.AuthorEmail
            );
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            return new PodcastCommentSubmitAttemptResult(
                false,
                "Pole Treść komentarza jest obowiązkowe.",
                PodcastCommentFormField.Content
            );
        }

        IsSubmitting = true;
        NotifyStateChanged();

        try
        {
            var result = await _wordPressCommentsService.SubmitCommentAsync(
                new WordPressCommentSubmissionRequest(
                    _postId,
                    AuthorName.Trim(),
                    AuthorEmail.Trim(),
                    Content.Trim(),
                    ReplyToCommentId
                ),
                cancellationToken
            );

            if (result.Accepted)
            {
                Content = string.Empty;
                CancelReply();
            }

            return new PodcastCommentSubmitAttemptResult(result.Accepted, result.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new PodcastCommentSubmitAttemptResult(
                false,
                "Nie udało się wysłać komentarza. Spróbuj ponownie później."
            );
        }
        finally
        {
            IsSubmitting = false;
            NotifyStateChanged();
        }
    }

    partial void OnAuthorNameChanged(string value)
    {
        PersistDraft(AuthorNameKey, value);
    }

    partial void OnAuthorEmailChanged(string value)
    {
        PersistDraft(AuthorEmailKey, value);
    }

    partial void OnIsSubmittingChanged(bool value)
    {
        NotifyStateChanged();
    }

    partial void OnReplyToCommentIdChanged(int value)
    {
        NotifyStateChanged();
    }

    partial void OnReplyToAuthorNameChanged(string? value)
    {
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsReplyMode));
        OnPropertyChanged(nameof(HasReplyTarget));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SubmitButtonText));
        OnPropertyChanged(nameof(FormHeadingText));
        OnPropertyChanged(nameof(ReplyTargetText));
        OnPropertyChanged(nameof(ReplyTargetVisibility));
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
