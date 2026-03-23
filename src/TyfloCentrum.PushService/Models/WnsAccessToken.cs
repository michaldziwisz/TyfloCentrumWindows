namespace TyfloCentrum.PushService.Models;

public sealed record WnsAccessToken(string Value, DateTimeOffset ExpiresAtUtc)
{
    public bool IsValid(DateTimeOffset nowUtc) => nowUtc < ExpiresAtUtc;
}
