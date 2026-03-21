using System.Text.Json.Serialization;

namespace Tyflocentrum.Windows.Domain.Models;

public sealed record FavoriteItem
{
    public string Id { get; init; } = string.Empty;

    public ContentSource Source { get; init; } = ContentSource.Podcast;

    public int PostId { get; init; }

    public FavoriteKind? Kind { get; init; }

    public FavoriteArticleOrigin? ArticleOrigin { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string PublishedDate { get; init; } = string.Empty;

    public string Link { get; init; } = string.Empty;

    public string ContextTitle { get; init; } = string.Empty;

    public string ContextSubtitle { get; init; } = string.Empty;

    public double? StartPositionSeconds { get; init; }

    public DateTimeOffset SavedAtUtc { get; init; }

    [JsonIgnore]
    public FavoriteKind ResolvedKind => Kind ?? DeriveKind(Source);

    [JsonIgnore]
    public FavoriteArticleOrigin ResolvedArticleOrigin =>
        ArticleOrigin ?? FavoriteArticleOrigin.Post;

    public static string CreateId(ContentSource source, int postId)
    {
        return $"{source}:{postId}";
    }

    public static string CreateId(
        ContentSource source,
        int postId,
        FavoriteArticleOrigin articleOrigin
    )
    {
        if (source == ContentSource.Article && articleOrigin == FavoriteArticleOrigin.Page)
        {
            return $"ArticlePage:{postId}";
        }

        return CreateId(source, postId);
    }

    public static string CreateTopicId(int podcastId, string title, double seconds)
    {
        return $"Topic:{podcastId}:{CreateHashKey($"{podcastId}|{seconds:0.###}|{title.ToLowerInvariant()}")}";
    }

    public static string CreateLinkId(int podcastId, string url)
    {
        return $"Link:{podcastId}:{CreateHashKey($"{podcastId}|{url.ToLowerInvariant()}")}";
    }

    private static FavoriteKind DeriveKind(ContentSource source)
    {
        return source == ContentSource.Article ? FavoriteKind.Article : FavoriteKind.Podcast;
    }

    private static string CreateHashKey(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value.Trim())
        );
        return Convert.ToHexString(bytes);
    }
}
