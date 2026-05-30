using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Text;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Domain;

public sealed class ShowNotesParserTests
{
    [Fact]
    public void Parse_extracts_markers_and_related_links_from_comment_html()
    {
        IReadOnlyList<WordPressComment> comments =
        [
            new WordPressComment
            {
                Id = 1001,
                PostId = 11,
                ParentId = 0,
                AuthorName = "TyfloPodcast",
                Content = new RenderedText(
                    "<p>Znaczniki czasu:</p><p>Intro 00:00:00<br>Temat tygodnia 12:34</p><p>A oto odnośniki uzupełniające audycję:</p><p>- Link testowy: https://example.com/test<br>- Kontakt: kontakt@example.com</p>"
                ),
            },
        ];

        var result = ShowNotesParser.Parse(comments);

        Assert.Collection(
            result.Markers,
            marker =>
            {
                Assert.Equal("Intro", marker.Title);
                Assert.Equal(0, marker.Seconds);
            },
            marker =>
            {
                Assert.Equal("Temat tygodnia", marker.Title);
                Assert.Equal(754, marker.Seconds);
            }
        );

        Assert.Collection(
            result.Links,
            link =>
            {
                Assert.Equal("Link testowy", link.Title);
                Assert.Equal("https://example.com/test", link.Url.AbsoluteUri);
            },
            link =>
            {
                Assert.Equal("Kontakt", link.Title);
                Assert.Equal("mailto:kontakt@example.com", link.Url.AbsoluteUri);
            }
        );
    }

    [Fact]
    public void Parse_deduplicates_markers_and_links()
    {
        IReadOnlyList<WordPressComment> comments =
        [
            new WordPressComment
            {
                Id = 1001,
                PostId = 11,
                ParentId = 0,
                AuthorName = "TyfloPodcast",
                Content = new RenderedText(
                    "<p>Znaczniki czasu:</p><p>Intro 00:00</p><p>Linki:</p><p>- Strona: https://example.com</p>"
                ),
            },
            new WordPressComment
            {
                Id = 1002,
                PostId = 11,
                ParentId = 0,
                AuthorName = "TyfloPodcast",
                Content = new RenderedText(
                    "<p>Znaczniki czasu:</p><p>Intro 00:00</p><p>Linki:</p><p>- Strona: https://example.com</p>"
                ),
            },
        ];

        var result = ShowNotesParser.Parse(comments);

        Assert.Single(result.Markers);
        Assert.Single(result.Links);
    }

    [Fact]
    public void ParseTextVersionLink_extracts_text_version_from_category_path()
    {
        var result = ShowNotesParser.ParseTextVersionLink(
            """
            <p>Audycja dostępna jest również w
            <a href="https://tyflopodcast.example/tekstowe-wersje-audycji/test-wersja-tekstowa/">wersji tekstowej</a>.</p>
            """
        );

        Assert.NotNull(result);
        Assert.Equal("Tekstowa wersja odcinka", result!.Title);
        Assert.Equal(
            "https://tyflopodcast.example/tekstowe-wersje-audycji/test-wersja-tekstowa/",
            result.Url.AbsoluteUri
        );
    }

    [Fact]
    public void ParseTextVersionLink_extracts_page_id_link_from_anchor_text()
    {
        var result = ShowNotesParser.ParseTextVersionLink(
            """
            <p>Dostępna jest <a href="/?page_id=11085">tekstowa wersja odcinka</a>.</p>
            """,
            new Uri("https://tyflopodcast.example/podcast-testowy/")
        );

        Assert.NotNull(result);
        Assert.Equal("https://tyflopodcast.example/?page_id=11085", result!.Url.AbsoluteUri);
    }

    [Fact]
    public void ParseTextVersionLink_ignores_unrelated_links()
    {
        var result = ShowNotesParser.ParseTextVersionLink(
            """
            <p><a href="https://example.com/projekt">Strona projektu</a></p>
            """
        );

        Assert.Null(result);
    }
}
