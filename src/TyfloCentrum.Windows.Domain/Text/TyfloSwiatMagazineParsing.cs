using System.Text.RegularExpressions;
using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Text;

public static partial class TyfloSwiatMagazineParsing
{
    public static (int? Number, int? Year) ParseIssueNumberAndYear(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (null, null);
        }

        var issueMatch = IssuePattern().Match(title);
        if (issueMatch.Success)
        {
            int? number = int.TryParse(issueMatch.Groups[1].Value, out var parsedNumber)
                ? parsedNumber
                : null;
            int? year = int.TryParse(issueMatch.Groups[2].Value, out var parsedYear)
                ? parsedYear
                : null;
            return (number, year);
        }

        var yearMatch = YearPattern().Match(title);
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var fallbackYear))
        {
            return (null, fallbackYear);
        }

        return (null, null);
    }

    public static string? ExtractFirstPdfUrl(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = PdfPattern().Match(html);
        return match.Success ? NormalizeLink(match.Groups[1].Value) : null;
    }

    public static IReadOnlyList<WpPostSummary> OrderedTableOfContents(
        IReadOnlyList<WpPostSummary> children,
        string issueHtml
    )
    {
        if (children.Count == 0)
        {
            return [];
        }

        var orderedLinks = ExtractLinks(issueHtml)
            .Select(NormalizeLink)
            .Where(link =>
                !link.Contains(".pdf", StringComparison.OrdinalIgnoreCase)
                && link.Contains("tyfloswiat.pl/czasopismo/", StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();

        var itemsByLink = children.ToDictionary(
            child => NormalizeLink(child.Link),
            child => child,
            StringComparer.OrdinalIgnoreCase
        );

        var seenIds = new HashSet<int>();
        var ordered = new List<WpPostSummary>();

        foreach (var link in orderedLinks)
        {
            if (!itemsByLink.TryGetValue(link, out var item))
            {
                continue;
            }

            if (!seenIds.Add(item.Id))
            {
                continue;
            }

            ordered.Add(item);
        }

        ordered.AddRange(
            children
                .Where(child => !seenIds.Contains(child.Id))
                .OrderByDescending(child => child.Date, StringComparer.Ordinal)
                .ThenByDescending(child => child.Id)
        );

        return ordered;
    }

    public static string NormalizeLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            trimmed = $"https://tyfloswiat.pl{trimmed}";
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri);
            if (string.Equals(builder.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                builder.Scheme = Uri.UriSchemeHttps;
                builder.Port = -1;
            }

            var normalized = builder.Uri.ToString().TrimEnd('/');
            return normalized;
        }

        return trimmed.TrimEnd('/');
    }

    private static IEnumerable<string> ExtractLinks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            yield break;
        }

        foreach (Match match in HrefPattern().Matches(html))
        {
            if (!match.Success)
            {
                continue;
            }

            yield return match.Groups[1].Value;
        }
    }

    [GeneratedRegex("(\\d{1,2})\\s*/\\s*(\\d{4})", RegexOptions.Compiled)]
    private static partial Regex IssuePattern();

    [GeneratedRegex("(19\\d{2}|20\\d{2})", RegexOptions.Compiled)]
    private static partial Regex YearPattern();

    [GeneratedRegex("href\\s*=\\s*['\\\"]([^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HrefPattern();

    [GeneratedRegex("href\\s*=\\s*['\\\"]([^'\\\"]+\\.pdf[^'\\\"]*)['\\\"]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PdfPattern();
}
