namespace TyfloCentrum.PushService.Models;

public sealed record PushNotificationPreferences(
    bool Podcast,
    bool Article,
    bool Live,
    bool Schedule
)
{
    public static PushNotificationPreferences Default { get; } = new(true, true, true, true);

    public bool IsEnabledFor(string category)
    {
        return category switch
        {
            PushCategories.Podcast => Podcast,
            PushCategories.Article => Article,
            PushCategories.Live => Live,
            PushCategories.Schedule => Schedule,
            _ => false,
        };
    }

    public static PushNotificationPreferences Normalize(PushNotificationPreferences? value)
    {
        return value ?? Default;
    }
}
