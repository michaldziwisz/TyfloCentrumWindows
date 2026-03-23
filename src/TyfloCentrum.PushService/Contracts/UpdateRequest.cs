using TyfloCentrum.PushService.Models;

namespace TyfloCentrum.PushService.Contracts;

public sealed record UpdateRequest(string Token, PushNotificationPreferences? Prefs);
