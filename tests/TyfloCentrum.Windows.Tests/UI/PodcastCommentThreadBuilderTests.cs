using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class PodcastCommentThreadBuilderTests
{
    [Fact]
    public void Build_orders_comments_from_oldest_to_newest_and_keeps_replies_under_parent()
    {
        IReadOnlyList<WordPressComment> comments =
        [
            CreateComment(1001, 0, "Komentarz 1", "2026-03-20T10:00:00"),
            CreateComment(1002, 0, "Komentarz 2", "2026-03-21T09:00:00"),
            CreateComment(1003, 1002, "Odpowiedź do komentarza 2", "2026-03-21T09:05:00"),
            CreateComment(1004, 0, "Komentarz 3", "2026-03-22T08:00:00"),
        ];

        var items = PodcastCommentThreadBuilder.Build(comments);

        var orderedIds = items.Select(item => item.Id).ToArray();

        Assert.Equal(new[] { 1001, 1002, 1003, 1004 }, orderedIds);

        Assert.Equal(0, items[0].ThreadDepth);
        Assert.Null(items[0].ReplyToAuthorName);

        Assert.Equal(0, items[1].ThreadDepth);
        Assert.Null(items[1].ReplyToAuthorName);

        Assert.Equal(1, items[2].ThreadDepth);
        Assert.Equal("Komentarz 2", items[2].ReplyToAuthorName);

        Assert.Equal(0, items[3].ThreadDepth);
        Assert.Null(items[3].ReplyToAuthorName);
    }

    [Fact]
    public void Build_reorders_newest_first_wordpress_payload_to_oldest_first_threaded_order()
    {
        IReadOnlyList<WordPressComment> comments =
        [
            CreateComment(1004, 0, "Komentarz 3", "2026-03-22T08:00:00"),
            CreateComment(1003, 1002, "Odpowiedź do komentarza 2", "2026-03-21T09:05:00"),
            CreateComment(1002, 0, "Komentarz 2", "2026-03-21T09:00:00"),
            CreateComment(1001, 0, "Komentarz 1", "2026-03-20T10:00:00"),
        ];

        var items = PodcastCommentThreadBuilder.Build(comments);

        Assert.Equal(new[] { 1001, 1002, 1003, 1004 }, items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void Build_uses_source_order_as_oldest_first_fallback_when_dates_are_missing()
    {
        IReadOnlyList<WordPressComment> comments =
        [
            CreateComment(1004, 0, "Komentarz 3", null),
            CreateComment(1003, 1002, "Odpowiedź do komentarza 2", null),
            CreateComment(1002, 0, "Komentarz 2", null),
            CreateComment(1001, 0, "Komentarz 1", null),
        ];

        var items = PodcastCommentThreadBuilder.Build(comments);

        Assert.Equal(new[] { 1001, 1002, 1003, 1004 }, items.Select(item => item.Id).ToArray());
    }

    private static WordPressComment CreateComment(
        int id,
        int parentId,
        string authorName,
        string? dateGmt
    )
    {
        return new WordPressComment
        {
            Id = id,
            PostId = 77,
            ParentId = parentId,
            AuthorName = authorName,
            DateGmt = dateGmt,
            Content = new RenderedText($"<p>{authorName}</p>"),
        };
    }
}
