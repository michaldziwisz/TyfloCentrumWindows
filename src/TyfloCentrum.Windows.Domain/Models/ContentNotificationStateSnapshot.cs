namespace TyfloCentrum.Windows.Domain.Models;

public sealed record ContentNotificationStateSnapshot(
    int? LastSeenPodcastPostId,
    int? LastSeenArticlePostId
)
{
    public static ContentNotificationStateSnapshot Empty { get; } = new(null, null);
}
