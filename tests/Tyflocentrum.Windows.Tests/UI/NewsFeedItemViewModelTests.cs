using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.UI.ViewModels;
using Xunit;

namespace Tyflocentrum.Windows.Tests.UI;

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
}
