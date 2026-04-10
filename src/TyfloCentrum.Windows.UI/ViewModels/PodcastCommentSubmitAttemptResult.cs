namespace TyfloCentrum.Windows.UI.ViewModels;

public readonly record struct PodcastCommentSubmitAttemptResult(
    bool Accepted,
    string Message,
    PodcastCommentFormField FocusTarget = PodcastCommentFormField.None
);
