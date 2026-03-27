using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class NewsFeedItemViewModelTests
{
    [Fact]
    public void ToString_returns_accessible_label_instead_of_type_name()
    {
        var viewModel = new NewsFeedItemViewModel(
            new NewsFeedItem(
                NewsItemKind.Podcast,
                new WpPostSummary
                {
                    Id = 1,
                    Date = "2026-03-19T10:15:00",
                    Link = "https://example.invalid/post/1",
                    Title = new RenderedText("Testowy tytul"),
                    Excerpt = new RenderedText("<p>Krotki opis</p>"),
                }
            )
        );

        Assert.Equal(viewModel.AccessibleLabel, viewModel.ToString());
        Assert.DoesNotContain(nameof(NewsFeedItemViewModel), viewModel.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("Testowy tytul.", viewModel.AccessibleLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("Podcast. Testowy tytul", viewModel.AccessibleLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultActionLabel_depends_on_news_item_kind()
    {
        var podcast = new NewsFeedItemViewModel(
            new NewsFeedItem(
                NewsItemKind.Podcast,
                new WpPostSummary
                {
                    Id = 2,
                    Date = "2026-03-19T10:15:00",
                    Link = "https://example.invalid/post/2",
                    Title = new RenderedText("Podcast dnia"),
                    Excerpt = new RenderedText("<p>Krotki opis</p>"),
                }
            )
        );
        var article = new NewsFeedItemViewModel(
            new NewsFeedItem(
                NewsItemKind.Article,
                new WpPostSummary
                {
                    Id = 3,
                    Date = "2026-03-19T10:15:00",
                    Link = "https://example.invalid/post/3",
                    Title = new RenderedText("Artykul dnia"),
                    Excerpt = new RenderedText("<p>Krotki opis</p>"),
                }
            )
        );

        Assert.Equal("Odtwórz podcast: Podcast dnia", podcast.DefaultActionLabel);
        Assert.Equal("Otwórz artykuł w aplikacji: Artykul dnia", article.DefaultActionLabel);
        Assert.Contains("zewnętrznej przeglądarce", podcast.OpenLinkLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibleLabel_can_place_news_item_kind_before_or_after_title()
    {
        var item = new NewsFeedItem(
            NewsItemKind.Article,
            new WpPostSummary
            {
                Id = 4,
                Date = "2026-03-19T10:15:00",
                Link = "https://example.invalid/post/4",
                Title = new RenderedText("Artykul dnia"),
                Excerpt = new RenderedText("<p>Krotki opis</p>"),
            }
        );
        var beforeTitle = new NewsFeedItemViewModel(
            item,
            ContentTypeAnnouncementPlacement.BeforeTitle
        );
        var afterTitle = new NewsFeedItemViewModel(
            item,
            ContentTypeAnnouncementPlacement.AfterTitle
        );

        Assert.StartsWith("Artykuł. Artykul dnia.", beforeTitle.AccessibleLabel, StringComparison.Ordinal);
        Assert.StartsWith("Artykul dnia. Artykuł.", afterTitle.AccessibleLabel, StringComparison.Ordinal);
    }
}
