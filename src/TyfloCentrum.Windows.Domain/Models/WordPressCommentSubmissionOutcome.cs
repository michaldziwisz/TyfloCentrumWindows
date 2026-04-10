namespace TyfloCentrum.Windows.Domain.Models;

public enum WordPressCommentSubmissionOutcome
{
    Published,
    PendingModeration,
    Spam,
    Accepted,
    Rejected,
}
