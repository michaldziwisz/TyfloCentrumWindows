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
}
