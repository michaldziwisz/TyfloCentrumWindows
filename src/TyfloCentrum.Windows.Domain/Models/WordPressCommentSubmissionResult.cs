namespace TyfloCentrum.Windows.Domain.Models;

public sealed record WordPressCommentSubmissionResult(
    bool Accepted,
    WordPressCommentSubmissionOutcome Outcome,
    string Message,
    string? WordPressCode = null,
    WordPressComment? Comment = null
);
