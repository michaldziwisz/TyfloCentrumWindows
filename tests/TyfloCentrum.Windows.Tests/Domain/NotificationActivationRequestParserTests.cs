using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Text;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Domain;

public sealed class NotificationActivationRequestParserTests
{
    [Fact]
    public void Parse_returns_request_for_article_arguments()
    {
        var request = NotificationActivationRequestParser.Parse(
            "kind=article&id=123&title=Nowy%20artykul&date=2026-03-21T12%3A00%3A00&link=https%3A%2F%2Fexample.invalid%2Fa"
        );

        Assert.NotNull(request);
        Assert.Equal(ContentSource.Article, request!.Source);
        Assert.Equal(123, request.PostId);
        Assert.Equal("Nowy artykul", request.Title);
        Assert.Equal("2026-03-21T12:00:00", request.PublishedDate);
        Assert.Equal("https://example.invalid/a", request.Link);
    }

    [Fact]
    public void Parse_returns_request_for_podcast_arguments()
    {
        var request = NotificationActivationRequestParser.Parse("kind=podcast&id=77&title=Nowy%20podcast");

        Assert.NotNull(request);
        Assert.Equal(ContentSource.Podcast, request!.Source);
        Assert.Equal(77, request.PostId);
        Assert.Equal("Nowy podcast", request.Title);
    }

    [Fact]
    public void Parse_returns_null_for_invalid_arguments()
    {
        Assert.Null(NotificationActivationRequestParser.Parse("kind=unknown&id=1"));
        Assert.Null(NotificationActivationRequestParser.Parse("kind=article&id=abc"));
        Assert.Null(NotificationActivationRequestParser.Parse(string.Empty));
    }
}
