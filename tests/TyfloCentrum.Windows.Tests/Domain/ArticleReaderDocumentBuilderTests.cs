using TyfloCentrum.Windows.Domain.Text;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Domain;

public sealed class ArticleReaderDocumentBuilderTests
{
    [Fact]
    public void Build_wraps_article_content_in_reader_document()
    {
        var html = ArticleReaderDocumentBuilder.Build(
            "Testowy artykuł",
            "20 marca 2026",
            "https://example.invalid/article",
            "<p>Pierwszy akapit.</p><p>Drugi akapit.</p>"
        );

        Assert.Contains("<main id=\"article-root\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Testowy artykuł</h1>", html, StringComparison.Ordinal);
        Assert.Contains("20 marca 2026", html, StringComparison.Ordinal);
        Assert.Contains("<article>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Pierwszy akapit.</p><p>Drugi akapit.</p>", html, StringComparison.Ordinal);
        Assert.Contains("window.chrome.webview.postMessage('close')", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_strips_script_and_style_tags_from_content()
    {
        var html = ArticleReaderDocumentBuilder.Build(
            "Test",
            "20 marca 2026",
            "https://example.invalid/article",
            "<style>body{display:none;}</style><p>Treść</p><script>alert('x');</script>"
        );

        Assert.Contains("<p>Treść</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("alert('x')", html, StringComparison.Ordinal);
        Assert.DoesNotContain("display:none", html, StringComparison.Ordinal);
    }
}
