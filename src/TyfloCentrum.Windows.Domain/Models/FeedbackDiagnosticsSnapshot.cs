namespace TyfloCentrum.Windows.Domain.Models;

public sealed record FeedbackDiagnosticsSnapshot(
    string AppVersion,
    string Build,
    string? Channel,
    string UserAgent,
    IReadOnlyDictionary<string, object?> PublicDiagnostics,
    FeedbackLogAttachment? LogAttachment
);
