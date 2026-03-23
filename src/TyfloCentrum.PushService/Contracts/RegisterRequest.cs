using TyfloCentrum.PushService.Models;

namespace TyfloCentrum.PushService.Contracts;

public sealed record RegisterRequest(string Token, string? Env, PushNotificationPreferences? Prefs);
