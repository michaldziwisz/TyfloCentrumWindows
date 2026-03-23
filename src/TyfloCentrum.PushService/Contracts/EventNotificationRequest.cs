namespace TyfloCentrum.PushService.Contracts;

public sealed record EventNotificationRequest(string? Title, string? StartedAt, string? UpdatedAt);
