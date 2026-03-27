using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class CommentItemViewModelTests
{
    [Fact]
    public void Constructor_creates_preview_and_keeps_full_body_for_details()
    {
        var longBody = new string('A', 300);
        var item = new CommentItemViewModel(new WordPressComment
        {
            Id = 1001,
            PostId = 11,
            ParentId = 0,
            AuthorName = "Słuchacz",
            Content = new RenderedText($"<p>{longBody}</p>"),
        });

        Assert.Equal(longBody, item.BodyText);
        Assert.NotEqual(longBody, item.BodyPreviewText);
        Assert.EndsWith("…", item.BodyPreviewText, StringComparison.Ordinal);
        Assert.True(item.HasTruncatedPreview);
    }

    [Fact]
    public void Constructor_uses_fallback_text_for_empty_comment_body()
    {
        var item = new CommentItemViewModel(new WordPressComment
        {
            Id = 1002,
            PostId = 11,
            ParentId = 0,
            AuthorName = "Słuchacz",
            Content = new RenderedText("<p>   </p>"),
        });

        Assert.Equal(string.Empty, item.BodyText);
        Assert.Equal("Brak treści komentarza.", item.BodyPreviewDisplayText);
        Assert.Equal("Słuchacz. Brak treści komentarza.", item.AccessibleLabel);
    }

    [Fact]
    public void ApplyThreadContext_marks_reply_and_updates_accessible_label()
    {
        var item = new CommentItemViewModel(new WordPressComment
        {
            Id = 1003,
            PostId = 11,
            ParentId = 1001,
            AuthorName = "Odpowiadający",
            Content = new RenderedText("<p>To jest odpowiedź.</p>"),
        });

        item.ApplyThreadContext(1, "Autor główny");

        Assert.True(item.IsReply);
        Assert.Equal("Odpowiedź do: Autor główny", item.ReplyContextText);
        Assert.Equal("Visible", item.ReplyContextVisibilityValue);
        Assert.Equal("24,0,0,8", item.ContainerMarginValue);
        Assert.Equal("3,0,0,0", item.ReplyAccentBorderThicknessValue);
        Assert.Contains("odpowiedź do Autor główny", item.AccessibleLabel, StringComparison.Ordinal);
    }
}
