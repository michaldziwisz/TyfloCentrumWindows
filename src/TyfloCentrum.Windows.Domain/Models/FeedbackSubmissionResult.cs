namespace TyfloCentrum.Windows.Domain.Models;

public sealed record FeedbackSubmissionResult(
    bool Success,
    string? ErrorMessage,
    string? PublicIssueUrl,
    string? ReportId
);
