using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class TyfloSwiatMagazineTocItemViewModelTests
{
    [Fact]
    public void AccessibleLabel_does_not_prefix_article_by_default()
    {
        var viewModel = new TyfloSwiatMagazineTocItemViewModel(
            new WpPostSummary
            {
                Id = 11,
                Date = "2026-03-20T10:00:00",
                Link = "https://example.invalid/page/11",
                Title = new RenderedText("Strona testowa"),
                Excerpt = new RenderedText(string.Empty),
            }
        );

        Assert.StartsWith("Strona testowa.", viewModel.AccessibleLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("Artykuł. Strona testowa", viewModel.AccessibleLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibleLabel_can_place_article_before_or_after_title()
    {
        var item = new WpPostSummary
        {
            Id = 12,
            Date = "2026-03-20T10:00:00",
            Link = "https://example.invalid/page/12",
            Title = new RenderedText("Strona testowa"),
            Excerpt = new RenderedText(string.Empty),
        };

        var beforeTitle = new TyfloSwiatMagazineTocItemViewModel(
            item,
            ContentTypeAnnouncementPlacement.BeforeTitle
        );
        var afterTitle = new TyfloSwiatMagazineTocItemViewModel(
            item,
            ContentTypeAnnouncementPlacement.AfterTitle
        );

        Assert.StartsWith("Artykuł. Strona testowa.", beforeTitle.AccessibleLabel, StringComparison.Ordinal);
        Assert.StartsWith("Strona testowa. Artykuł.", afterTitle.AccessibleLabel, StringComparison.Ordinal);
    }
}
