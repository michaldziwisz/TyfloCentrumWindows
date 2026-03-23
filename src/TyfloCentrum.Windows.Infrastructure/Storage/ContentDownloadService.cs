using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;
using TyfloCentrum.Windows.Infrastructure.Http;

namespace TyfloCentrum.Windows.Infrastructure.Storage;

public sealed partial class ContentDownloadService : IContentDownloadService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IDownloadDirectoryService _downloadDirectoryService;
    private readonly HttpClient _httpClient;
    private readonly ITyfloSwiatMagazineService _magazineService;
    private readonly TyfloCentrumEndpointsOptions _options;
    private readonly IWordPressPostDetailsService _postDetailsService;

    public ContentDownloadService(
        HttpClient httpClient,
        IAppSettingsService appSettingsService,
        IDownloadDirectoryService downloadDirectoryService,
        IWordPressPostDetailsService postDetailsService,
        ITyfloSwiatMagazineService magazineService,
        TyfloCentrumEndpointsOptions options
    )
    {
        _httpClient = httpClient;
        _appSettingsService = appSettingsService;
        _downloadDirectoryService = downloadDirectoryService;
        _postDetailsService = postDetailsService;
        _magazineService = magazineService;
        _options = options;
    }

    public async Task<string> DownloadPodcastAsync(
        int postId,
        string title,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postId);

        var targetDirectory = await ResolveTargetDirectoryAsync(cancellationToken);
        var builder = new UriBuilder(_options.TyflopodcastDownloadUrl)
        {
            Query = $"id={postId}&plik=0",
        };

        var filePath = BuildUniqueFilePath(
            targetDirectory,
            DownloadFileNameSanitizer.CreateFileName(title, $"Podcast {postId}", ".mp3")
        );

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        await using var output = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None
        );
        await response.Content.CopyToAsync(output, cancellationToken);

        return filePath;
    }

    public async Task<string> DownloadArticleAsync(
        ContentSource source,
        int postId,
        string title,
        string fallbackDate,
        string fallbackLink,
        CancellationToken cancellationToken = default
    )
    {
        if (source != ContentSource.Article)
        {
            throw new InvalidOperationException("Pobieranie artykułu jest dostępne tylko dla treści artykułowych.");
        }

        var post = await _postDetailsService.GetPostAsync(source, postId, cancellationToken);
        return await SaveArticleAsync(
            NormalizeTitle(post.Title.Rendered, title),
            string.IsNullOrWhiteSpace(post.Date) ? fallbackDate : post.Date,
            string.IsNullOrWhiteSpace(post.Link) ? fallbackLink : post.Link,
            post.Content.Rendered,
            cancellationToken
        );
    }

    public async Task<string> DownloadTyfloSwiatPageAsync(
        int pageId,
        string title,
        string fallbackDate,
        string fallbackLink,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageId);

        var page = await _magazineService.GetPageAsync(pageId, cancellationToken);
        return await SaveArticleAsync(
            NormalizeTitle(page.Title.Rendered, title),
            string.IsNullOrWhiteSpace(page.Date) ? fallbackDate : page.Date,
            string.IsNullOrWhiteSpace(page.Link) ? fallbackLink : page.Link,
            page.Content.Rendered,
            cancellationToken
        );
    }

    private async Task<string> SaveArticleAsync(
        string title,
        string publishedDate,
        string externalUrl,
        string contentHtml,
        CancellationToken cancellationToken
    )
    {
        var targetDirectory = await ResolveTargetDirectoryAsync(cancellationToken);
        var embeddedHtml = await InlineImagesAsync(contentHtml, externalUrl, cancellationToken);
        var documentHtml = ArticleReaderDocumentBuilder.Build(
            title,
            publishedDate,
            externalUrl,
            embeddedHtml
        );
        var filePath = BuildUniqueFilePath(
            targetDirectory,
            DownloadFileNameSanitizer.CreateFileName(title, "Artykuł", ".html")
        );

        await File.WriteAllTextAsync(filePath, documentHtml, new UTF8Encoding(true), cancellationToken);
        return filePath;
    }

    private async Task<string> ResolveTargetDirectoryAsync(CancellationToken cancellationToken)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        var targetDirectory = _downloadDirectoryService.GetEffectiveDownloadDirectoryPath(
            settings.DownloadDirectoryPath
        );
        Directory.CreateDirectory(targetDirectory);
        return targetDirectory;
    }

    private static string BuildUniqueFilePath(string directoryPath, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var candidatePath = Path.Combine(directoryPath, fileName);

        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }

        for (var attempt = 2; attempt < 10_000; attempt++)
        {
            var alternateName = $"{baseName} ({attempt}){extension}";
            candidatePath = Path.Combine(directoryPath, alternateName);
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException("Nie udało się wygenerować unikalnej nazwy pliku do pobrania.");
    }

    private async Task<string> InlineImagesAsync(
        string contentHtml,
        string? baseAddress,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(contentHtml))
        {
            return contentHtml;
        }

        var baseUri = TryCreateUri(baseAddress, null);
        var matches = ImageTagRegex().Matches(contentHtml);
        if (matches.Count == 0)
        {
            return contentHtml;
        }

        var cache = new Dictionary<string, string?>(StringComparer.Ordinal);
        var builder = new StringBuilder(contentHtml.Length + 256);
        var lastIndex = 0;

        foreach (Match match in matches)
        {
            builder.Append(contentHtml, lastIndex, match.Index - lastIndex);
            var tag = match.Value;
            var originalSource = match.Groups["src"].Value;

            if (!cache.TryGetValue(originalSource, out var embeddedSource))
            {
                embeddedSource = await TryInlineImageAsync(originalSource, baseUri, cancellationToken);
                cache[originalSource] = embeddedSource;
            }

            if (!string.IsNullOrWhiteSpace(embeddedSource))
            {
                var updatedTag = SrcAttributeRegex().Replace(
                    tag,
                    $"{match.Groups["prefix"].Value}{embeddedSource}{match.Groups["suffix"].Value}",
                    1
                );
                updatedTag = SrcSetAttributeRegex().Replace(updatedTag, string.Empty);
                builder.Append(updatedTag);
            }
            else
            {
                builder.Append(tag);
            }

            lastIndex = match.Index + match.Length;
        }

        builder.Append(contentHtml, lastIndex, contentHtml.Length - lastIndex);
        return builder.ToString();
    }

    private async Task<string?> TryInlineImageAsync(
        string originalSource,
        Uri? baseUri,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(originalSource))
        {
            return null;
        }

        var source = WebUtility.HtmlDecode(originalSource.Trim());
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var imageUri = TryCreateUri(source, baseUri);
        if (imageUri is null)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                imageUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0)
            {
                return null;
            }

            var mediaType =
                response.Content.Headers.ContentType?.MediaType
                ?? InferMediaTypeFromExtension(imageUri.AbsolutePath);
            return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return null;
        }
    }

    private static Uri? TryCreateUri(string? value, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        return baseUri is not null && Uri.TryCreate(baseUri, value, out var relativeUri)
            ? relativeUri
            : null;
    }

    private static string InferMediaTypeFromExtension(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".avif" => "image/avif",
            _ => "image/jpeg",
        };
    }

    private static string NormalizeTitle(string? renderedTitle, string fallbackTitle)
    {
        var title = WordPressContentText.NormalizeHtml(renderedTitle ?? string.Empty);
        return string.IsNullOrWhiteSpace(title)
            ? (string.IsNullOrWhiteSpace(fallbackTitle) ? "Artykuł" : fallbackTitle.Trim())
            : title;
    }

    [GeneratedRegex(
        @"(?is)<img\b[^>]*?(?<prefix>\bsrc\s*=\s*['""])(?<src>.*?)(?<suffix>['""])[^>]*>",
        RegexOptions.Compiled
    )]
    private static partial Regex ImageTagRegex();

    [GeneratedRegex(@"(?is)(?<prefix>\bsrc\s*=\s*['""])(?<src>.*?)(?<suffix>['""])", RegexOptions.Compiled)]
    private static partial Regex SrcAttributeRegex();

    [GeneratedRegex(@"\s+srcset\s*=\s*(['""]).*?\1", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SrcSetAttributeRegex();
}
