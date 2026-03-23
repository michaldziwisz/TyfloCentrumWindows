namespace TyfloCentrum.Windows.Domain.Models;

public sealed record NotificationActivationRequest(
    ContentSource Source,
    int PostId,
    string Title,
    string? PublishedDate,
    string? Link
);
