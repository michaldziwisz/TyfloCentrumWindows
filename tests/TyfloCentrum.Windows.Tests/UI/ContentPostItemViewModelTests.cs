using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContentPostItemViewModelTests
{
    [Fact]
    public void ToString_returns_accessible_label_instead_of_type_name()
    {
        var viewModel = new ContentPostItemViewModel(
            ContentSource.Article,
            new WpPostSummary
            {
                Id = 21,
                Date = "2026-03-19T11:15:00",
                Link = "https://example.invalid/post/21",
                Title = new RenderedText("Artykuł testowy"),
                Excerpt = new RenderedText("<p>Krótki opis</p>"),
            }
        );

        Assert.Equal(viewModel.AccessibleLabel, viewModel.ToString());
        Assert.DoesNotContain(nameof(ContentPostItemViewModel), viewModel.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("Artykuł testowy.", viewModel.AccessibleLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("Artykuł. Artykuł testowy", viewModel.AccessibleLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultActionLabel_depends_on_content_source()
    {
        var podcast = new ContentPostItemViewModel(
            ContentSource.Podcast,
            new WpPostSummary
            {
                Id = 7,
                Date = "2026-03-19T11:15:00",
                Link = "https://example.invalid/post/7",
                Title = new RenderedText("Podcast testowy"),
                Excerpt = new RenderedText("<p>Krótki opis</p>"),
            }
        );
        var article = new ContentPostItemViewModel(
            ContentSource.Article,
            new WpPostSummary
            {
                Id = 8,
                Date = "2026-03-19T11:15:00",
                Link = "https://example.invalid/post/8",
                Title = new RenderedText("Artykuł testowy"),
                Excerpt = new RenderedText("<p>Krótki opis</p>"),
            }
        );

        Assert.Equal("Odtwórz podcast: Podcast testowy", podcast.DefaultActionLabel);
        Assert.Equal("Otwórz artykuł w aplikacji: Artykuł testowy", article.DefaultActionLabel);
        Assert.Contains("zewnętrznej przeglądarce", article.OpenLinkLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibleLabel_can_place_content_type_before_or_after_title()
    {
        var item = new WpPostSummary
        {
            Id = 9,
            Date = "2026-03-19T11:15:00",
            Link = "https://example.invalid/post/9",
            Title = new RenderedText("Podcast testowy"),
            Excerpt = new RenderedText("<p>Krótki opis</p>"),
        };
        var beforeTitle = new ContentPostItemViewModel(
            ContentSource.Podcast,
            item,
            ContentTypeAnnouncementPlacement.BeforeTitle
        );
        var afterTitle = new ContentPostItemViewModel(
            ContentSource.Podcast,
            item,
            ContentTypeAnnouncementPlacement.AfterTitle
        );

        Assert.StartsWith("Podcast. Podcast testowy.", beforeTitle.AccessibleLabel, StringComparison.Ordinal);
        Assert.StartsWith("Podcast testowy. Podcast.", afterTitle.AccessibleLabel, StringComparison.Ordinal);
    }
}
