namespace TyfloCentrum.Windows.Domain.Models;

public sealed record FeedbackSubmissionRequest(
    FeedbackSubmissionKind Kind,
    string Title,
    string Description,
    string? ContactEmail,
    bool IncludeDiagnostics,
    bool IncludeLogFile
);
