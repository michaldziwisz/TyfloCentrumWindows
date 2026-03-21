using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tyflocentrum.Windows.Domain.Text;

public static partial class WordPressContentText
{
    public static string NormalizeHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var stripped = HtmlTagRegexFactory().Replace(decoded, " ");
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    public static string NormalizeForSearch(string value)
    {
        var builder = new StringBuilder();
        var normalized = NormalizeHtml(value).Normalize(NormalizationForm.FormD);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(MapCharacter(character));
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Trim();
    }

    public static string ToReadablePlainText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedBreaks = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        normalizedBreaks = Regex.Replace(normalizedBreaks, @"(?i)<br\s*/?>", "\n");
        normalizedBreaks = Regex.Replace(
            normalizedBreaks,
            @"(?i)</(p|div|section|article|h1|h2|h3|h4|h5|h6|li|ul|ol|blockquote|tr)>",
            "\n"
        );
        normalizedBreaks = Regex.Replace(normalizedBreaks, @"(?i)<li[^>]*>", "- ");

        var decoded = WebUtility.HtmlDecode(normalizedBreaks);
        var stripped = HtmlTagRegexFactory().Replace(decoded, " ");
        stripped = Regex.Replace(stripped, @"[^\S\n]+", " ");
        stripped = Regex.Replace(stripped, @" *\n *", "\n");
        var lines = stripped
            .Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0);

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static char MapCharacter(char value)
    {
        return char.ToLowerInvariant(value) switch
        {
            'ł' => 'l',
            _ => char.ToLowerInvariant(value),
        };
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegexFactory();
}
