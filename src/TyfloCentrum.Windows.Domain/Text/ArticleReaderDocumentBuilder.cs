using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace TyfloCentrum.Windows.Domain.Text;

public static partial class ArticleReaderDocumentBuilder
{
    public static string Build(
        string title,
        string publishedDate,
        string? externalUrl,
        string contentHtml
    )
    {
        var safeTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(title) ? "Artykuł" : title.Trim());
        var safeDate = string.IsNullOrWhiteSpace(publishedDate)
            ? string.Empty
            : WebUtility.HtmlEncode(publishedDate.Trim());
        var safeExternalUrl = string.IsNullOrWhiteSpace(externalUrl)
            ? string.Empty
            : WebUtility.HtmlEncode(externalUrl.Trim());
        var safeContent = PrepareContent(contentHtml);

        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"pl\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine($"  <title>{safeTitle}</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    :root { color-scheme: light; }");
        builder.AppendLine(
            "    html, body { margin: 0; padding: 0; background: #f6f1e7; color: #171412; font-family: Georgia, \"Times New Roman\", serif; line-height: 1.75; }"
        );
        builder.AppendLine(
            "    body { padding: 28px 20px 40px; } main { max-width: 860px; margin: 0 auto; background: #fffdfa; border: 1px solid #d6cdc0; border-radius: 18px; box-shadow: 0 16px 40px rgba(78, 60, 38, 0.08); padding: 28px 24px 36px; outline: none; }"
        );
        builder.AppendLine(
            "    h1 { margin: 0 0 12px; font-size: 2rem; line-height: 1.2; color: #201911; } .meta { margin: 0 0 24px; color: #5a4a37; font-size: 1rem; } article { font-size: 1.16rem; } article :first-child { margin-top: 0; } p, li, blockquote { margin: 0 0 1em; } ul, ol { padding-left: 1.4em; } blockquote { border-left: 4px solid #b8874a; margin-left: 0; padding-left: 1em; color: #4d3b27; } a { color: #8c3f17; text-decoration-thickness: 0.12em; } a:focus, a:hover { color: #5a2409; } img, figure { max-width: 100%; height: auto; } table { width: 100%; border-collapse: collapse; margin: 1.2em 0; } th, td { border: 1px solid #cdb79d; padding: 0.5em 0.65em; text-align: left; vertical-align: top; } code, pre { font-family: Consolas, \"Courier New\", monospace; } pre { white-space: pre-wrap; background: #f2ede4; padding: 1em; border-radius: 12px; overflow-wrap: anywhere; } :focus { outline: 3px solid #0f6cbd; outline-offset: 3px; }"
        );
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <main>");
        builder.AppendLine("    <div id=\"article-root\" tabindex=\"-1\">");
        builder.AppendLine("      <header>");
        builder.AppendLine($"        <h1>{safeTitle}</h1>");
        if (!string.IsNullOrWhiteSpace(safeDate) || !string.IsNullOrWhiteSpace(safeExternalUrl))
        {
            builder.Append("        <p class=\"meta\">");
            if (!string.IsNullOrWhiteSpace(safeDate))
            {
                builder.Append(safeDate);
            }

            if (!string.IsNullOrWhiteSpace(safeExternalUrl))
            {
                if (!string.IsNullOrWhiteSpace(safeDate))
                {
                    builder.Append(" · ");
                }

                builder.Append($"<a href=\"{safeExternalUrl}\">Otwórz oryginalny adres</a>");
            }

            builder.AppendLine("</p>");
        }
        builder.AppendLine("      </header>");
        builder.AppendLine("      <article>");
        builder.AppendLine(safeContent);
        builder.AppendLine("      </article>");
        builder.AppendLine("    </div>");
        builder.AppendLine("  </main>");
        builder.AppendLine("  <script>");
        builder.AppendLine("    (function () {");
        builder.AppendLine("      const root = document.getElementById('article-root');");
        builder.AppendLine("      if (root) { setTimeout(() => root.focus(), 0); }");
        builder.AppendLine("      document.addEventListener('keydown', event => {");
        builder.AppendLine(
            "        if (event.key === 'Escape' && window.chrome && window.chrome.webview) { event.preventDefault(); window.chrome.webview.postMessage('close'); }"
        );
        builder.AppendLine("      });");
        builder.AppendLine("      document.addEventListener('click', event => {");
        builder.AppendLine("        const anchor = event.target.closest('a[href]');");
        builder.AppendLine("        if (!anchor) { return; }");
        builder.AppendLine("        const href = anchor.getAttribute('href');");
        builder.AppendLine("        if (!href || !window.chrome || !window.chrome.webview) { return; }");
        builder.AppendLine(
            "        event.preventDefault(); window.chrome.webview.postMessage('openExternal:' + href);"
        );
        builder.AppendLine("      });");
        builder.AppendLine("    }());");
        builder.AppendLine("  </script>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string PrepareContent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<p>Brak treści artykułu.</p>";
        }

        var withoutScripts = ScriptRegexFactory().Replace(value, string.Empty);
        var withoutStyles = StyleRegexFactory().Replace(withoutScripts, string.Empty);
        var normalized = withoutStyles.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "<p>Brak treści artykułu.</p>" : normalized;
    }

    [GeneratedRegex(@"(?is)<script\b[^>]*>.*?</script>", RegexOptions.Compiled)]
    private static partial Regex ScriptRegexFactory();

    [GeneratedRegex(@"(?is)<style\b[^>]*>.*?</style>", RegexOptions.Compiled)]
    private static partial Regex StyleRegexFactory();
}
