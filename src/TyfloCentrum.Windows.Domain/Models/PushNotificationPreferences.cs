namespace TyfloCentrum.Windows.Domain.Models;

public sealed record PushNotificationPreferences(
    bool Podcast,
    bool Article,
    bool Live,
    bool Schedule
)
{
    public static PushNotificationPreferences FromSettings(AppSettingsSnapshot settings)
    {
        return new PushNotificationPreferences(
            settings.NotifyAboutNewPodcasts,
            settings.NotifyAboutNewArticles,
            false,
            false
        );
    }

    public bool AnyEnabled => Podcast || Article || Live || Schedule;
}
