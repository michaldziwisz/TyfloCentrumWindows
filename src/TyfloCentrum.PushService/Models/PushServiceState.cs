namespace TyfloCentrum.PushService.Models;

public sealed class PushServiceState
{
    public Dictionary<string, PushSubscriberRecord> Tokens { get; set; } = [];

    public List<int> SentPodcastIds { get; set; } = [];

    public List<int> SentArticleIds { get; set; } = [];

    public bool PodcastBaselineInitialized { get; set; }

    public bool ArticleBaselineInitialized { get; set; }

    public PushServiceState Clone()
    {
        return new PushServiceState
        {
            Tokens = Tokens.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal
            ),
            SentPodcastIds = [.. SentPodcastIds],
            SentArticleIds = [.. SentArticleIds],
            PodcastBaselineInitialized = PodcastBaselineInitialized,
            ArticleBaselineInitialized = ArticleBaselineInitialized,
        };
    }
}
