using System.Text.RegularExpressions;
using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Text;

public static partial class ShowNotesParser
{
    public static ShowNotesParseResult Parse(IReadOnlyList<WordPressComment> comments)
    {
        var markers = new List<ChapterMarker>();
        var links = new List<RelatedLink>();

        foreach (var comment in comments)
        {
            var lines = NormalizeLines(comment.Content.Rendered);
            if (lines.Count == 0)
            {
                continue;
            }

            if (ParseMarkers(lines) is { Count: > 0 } parsedMarkers)
            {
                markers.AddRange(parsedMarkers);
            }

            if (ParseLinks(lines) is { Count: > 0 } parsedLinks)
            {
                links.AddRange(parsedLinks);
            }
        }

        return new ShowNotesParseResult(
            UniqueMarkers(markers).OrderBy(marker => marker.Seconds).ToArray(),
            UniqueLinks(links)
        );
    }

    private static IReadOnlyList<string> NormalizeLines(string html)
    {
        return WordPressContentText
            .ToReadablePlainText(html)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static IReadOnlyList<ChapterMarker>? ParseMarkers(IReadOnlyList<string> lines)
    {
        var headerIndex = lines
            .Select((line, index) => new { line, index })
            .FirstOrDefault(item => IsMarkersHeader(item.line))
            ?.index;

        if (headerIndex is null)
        {
            return null;
        }

        var markers = new List<ChapterMarker>();
        for (var index = headerIndex.Value + 1; index < lines.Count; index++)
        {
            var marker = ParseMarkerLine(lines[index]);
            if (marker is not null)
            {
                markers.Add(marker);
            }
        }

        return markers.Count == 0 ? null : markers;
    }

    private static bool IsMarkersHeader(string line)
    {
        var normalized = line.Trim().ToLowerInvariant();
        return normalized.StartsWith("znaczniki czasu", StringComparison.Ordinal)
            || normalized.StartsWith("znaczniki czasowe", StringComparison.Ordinal);
    }

    private static ChapterMarker? ParseMarkerLine(string line)
    {
        var match = TimecodeRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var timeString = match.Value;
        var seconds = ParseTimecode(timeString);
        if (seconds is null)
        {
            return null;
        }

        var title = line[..match.Index]
            .Trim()
            .Trim('–', '—', '-', ':')
            .Trim();

        return string.IsNullOrWhiteSpace(title) ? null : new ChapterMarker(title, seconds.Value);
    }

    private static double? ParseTimecode(string timecode)
    {
        var parts = timecode.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length is not (2 or 3))
        {
            return null;
        }

        var numbers = parts.Select(part => int.TryParse(part, out var value) ? value : -1).ToArray();
        if (numbers.Any(value => value < 0))
        {
            return null;
        }

        return numbers.Length == 3
            ? numbers[0] * 3600 + numbers[1] * 60 + numbers[2]
            : numbers[0] * 60 + numbers[1];
    }

    private static IReadOnlyList<RelatedLink>? ParseLinks(IReadOnlyList<string> lines)
    {
        var headerIndex = lines
            .Select((line, index) => new { line, index })
            .FirstOrDefault(item => IsLinksHeader(item.line))
            ?.index;

        if (headerIndex is null)
        {
            return null;
        }

        var links = new List<RelatedLink>();
        string? currentTitle = null;
        var currentUrls = new List<Uri>();

        void FlushCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentTitle))
            {
                currentUrls.Clear();
                return;
            }

            var dedupedUrls = DedupUrls(currentUrls);
            foreach (var url in dedupedUrls)
            {
                var title = BuildLinkTitle(currentTitle, url, dedupedUrls.Count > 1);
                links.Add(new RelatedLink(title, url));
            }

            currentUrls.Clear();
        }

        for (var index = headerIndex.Value + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            var bullet = ParseBulletTitle(line);
            if (bullet is not null)
            {
                FlushCurrent();
                currentTitle = bullet.Value.Title;
                currentUrls.AddRange(bullet.Value.Urls);
                continue;
            }

            var urls = ExtractUrls(line);
            if (urls.Count > 0)
            {
                currentUrls.AddRange(urls);
                continue;
            }

            if (ParseEmailUrl(line) is { } emailUrl)
            {
                FlushCurrent();
                var label = ParseLeadingLabel(line) ?? "E-mail";
                links.Add(new RelatedLink(label, emailUrl));
            }
        }

        FlushCurrent();
        return links.Count == 0 ? null : links;
    }

    private static bool IsLinksHeader(string line)
    {
        var normalized = line.ToLowerInvariant();
        return normalized.Contains("odnośnik", StringComparison.Ordinal)
            || normalized.Contains("odnosnik", StringComparison.Ordinal)
            || normalized.Contains("odnośniki", StringComparison.Ordinal)
            || normalized.Contains("linki", StringComparison.Ordinal);
    }

    private static (string Title, IReadOnlyList<Uri> Urls)? ParseBulletTitle(string line)
    {
        var trimmed = line.Trim();
        if (!(trimmed.StartsWith('–') || trimmed.StartsWith('-')))
        {
            return null;
        }

        var rest = trimmed[1..].Trim();
        var urls = ExtractUrls(rest).ToList();
        var emailMatch = EmailRegex().Match(rest);
        if (emailMatch.Success && Uri.TryCreate($"mailto:{emailMatch.Value}", UriKind.Absolute, out var emailUrl))
        {
            urls.Add(emailUrl);
        }

        var title = rest;
        foreach (var url in urls)
        {
            title = title.Replace(url.AbsoluteUri, string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        if (emailMatch.Success)
        {
            title = title.Replace(emailMatch.Value, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        title = title.Trim().Trim(':').Trim();
        return string.IsNullOrWhiteSpace(title) ? null : (title, urls);
    }

    private static IReadOnlyList<Uri> ExtractUrls(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var urls = new List<Uri>();

        foreach (var token in tokens)
        {
            var raw = token.Trim().TrimEnd('.', ',', ')', ';', ']', '"', '\'');
            if (
                !raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            if (Uri.TryCreate(raw, UriKind.Absolute, out var url))
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    private static Uri? ParseEmailUrl(string line)
    {
        if (!line.Contains('@'))
        {
            return null;
        }

        var compact = line.Replace(" ", string.Empty, StringComparison.Ordinal);
        var match = EmailRegex().Match(compact);
        if (!match.Success)
        {
            return null;
        }

        return Uri.TryCreate($"mailto:{match.Value}", UriKind.Absolute, out var url) ? url : null;
    }

    private static string? ParseLeadingLabel(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
        {
            return null;
        }

        var label = line[..colonIndex].Trim().TrimStart('-', '–').Trim();
        return string.IsNullOrWhiteSpace(label) ? null : label;
    }

    private static string BuildLinkTitle(string baseTitle, Uri url, bool disambiguate)
    {
        if (!disambiguate)
        {
            return baseTitle;
        }

        var suffix = url.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase)
            ? "e-mail"
            : url.Host;

        return string.IsNullOrWhiteSpace(suffix) ? baseTitle : $"{baseTitle} ({suffix})";
    }

    private static IReadOnlyList<Uri> DedupUrls(IReadOnlyList<Uri> urls)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Uri>();

        foreach (var url in urls)
        {
            if (!seen.Add(url.AbsoluteUri))
            {
                continue;
            }

            result.Add(url);
        }

        return result;
    }

    private static IReadOnlyList<ChapterMarker> UniqueMarkers(IReadOnlyList<ChapterMarker> markers)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ChapterMarker>();

        foreach (var marker in markers)
        {
            var key = $"{(int)marker.Seconds}|{marker.Title}";
            if (!seen.Add(key))
            {
                continue;
            }

            result.Add(marker);
        }

        return result;
    }

    private static IReadOnlyList<RelatedLink> UniqueLinks(IReadOnlyList<RelatedLink> links)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RelatedLink>();

        foreach (var link in links)
        {
            var key = $"{link.Title}|{link.Url.AbsoluteUri}";
            if (!seen.Add(key))
            {
                continue;
            }

            result.Add(link);
        }

        return result;
    }

    [GeneratedRegex(@"(?:\b\d{1,2}:\d{2}:\d{2}\b|\b\d{1,2}:\d{2}\b)$", RegexOptions.Compiled)]
    private static partial Regex TimecodeRegex();

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
