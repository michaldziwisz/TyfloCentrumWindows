namespace TyfloCentrum.PushService.Models;

public sealed record PushDispatchPayload(
    string Kind,
    string Title,
    string Body,
    int? Id = null,
    string? Date = null,
    string? Link = null
);
