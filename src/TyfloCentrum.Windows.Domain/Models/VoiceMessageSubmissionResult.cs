namespace TyfloCentrum.Windows.Domain.Models;

public sealed record VoiceMessageSubmissionResult(
    bool Success,
    string? ErrorMessage,
    int? DurationMs
);
