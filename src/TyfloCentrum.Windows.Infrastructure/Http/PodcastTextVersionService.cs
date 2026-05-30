using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class PodcastTextVersionService : IPodcastTextVersionService
{
    private readonly IWordPressPageDetailsService _pageDetailsService;

    public PodcastTextVersionService(IWordPressPageDetailsService pageDetailsService)
    {
        _pageDetailsService = pageDetailsService;
    }

    public async Task<PodcastTextVersionDocument?> GetAsync(
        RelatedLink textVersionLink,
        string fallbackTitle,
        string fallbackDate,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var page = await ResolvePageAsync(textVersionLink.Url, cancellationToken);
            if (page is null)
            {
                return null;
            }

            var title = WordPressContentText.NormalizeHtml(page.Title.Rendered);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = string.IsNullOrWhiteSpace(fallbackTitle)
                    ? "Wersja tekstowa odcinka"
                    : fallbackTitle.Trim();
            }

            var link = string.IsNullOrWhiteSpace(page.Link)
                ? textVersionLink.Url.AbsoluteUri
                : page.Link.Trim();
            var publishedDate = string.IsNullOrWhiteSpace(page.Date)
                ? fallbackDate
                : page.Date.Trim();
            var readerHtml = ArticleReaderDocumentBuilder.Build(
                title,
                publishedDate,
                link,
                page.Content.Rendered
            );

            return new PodcastTextVersionDocument(title, publishedDate, link, readerHtml);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<WpPostDetail?> ResolvePageAsync(
        Uri textVersionUrl,
        CancellationToken cancellationToken
    )
    {
        if (TryGetPageId(textVersionUrl, out var pageId))
        {
            return await _pageDetailsService.GetPageAsync(
                ContentSource.Podcast,
                pageId,
                cancellationToken
            );
        }

        var slug = GetLastPathSegment(textVersionUrl);
        return slug is null
            ? null
            : await _pageDetailsService.GetPageBySlugAsync(
                ContentSource.Podcast,
                slug,
                cancellationToken
            );
    }

    private static bool TryGetPageId(Uri url, out int pageId)
    {
        pageId = 0;
        var query = url.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[0]);
            if (!string.Equals(key, "page_id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(pair[1]);
            return int.TryParse(value, out pageId) && pageId > 0;
        }

        return false;
    }

    private static string? GetLastPathSegment(Uri url)
    {
        var segments = url
            .AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length == 0 ? null : Uri.UnescapeDataString(segments[^1]);
    }
}
