namespace TyfloCentrum.PushService.Models;

public sealed class PushSubscriberRecord
{
    public string Token { get; set; } = string.Empty;

    public string Env { get; set; } = "unknown";

    public PushNotificationPreferences Prefs { get; set; } = PushNotificationPreferences.Default;

    public string CreatedAt { get; set; } = string.Empty;

    public string UpdatedAt { get; set; } = string.Empty;

    public string LastSeenAt { get; set; } = string.Empty;

    public string? LastNotifiedAt { get; set; }

    public PushSubscriberRecord Clone()
    {
        return new PushSubscriberRecord
        {
            Token = Token,
            Env = Env,
            Prefs = Prefs with { },
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            LastSeenAt = LastSeenAt,
            LastNotifiedAt = LastNotifiedAt,
        };
    }
}
