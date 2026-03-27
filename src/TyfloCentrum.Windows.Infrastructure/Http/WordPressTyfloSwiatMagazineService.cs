using System.Net.Http.Json;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class WordPressTyfloSwiatMagazineService : ITyfloSwiatMagazineService
{
    private const int MagazineRootPageId = 1409;
    private static readonly TimeSpan IssuesCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan IssueDetailCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITransientContentCache _cache;
    private readonly HttpClient _httpClient;
    private readonly TyfloCentrumEndpointsOptions _options;

    public WordPressTyfloSwiatMagazineService(
        HttpClient httpClient,
        TyfloCentrumEndpointsOptions options,
        ITransientContentCache cache
    )
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<IReadOnlyList<WpPostSummary>> GetIssuesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var directIssues = await GetPageSummariesByParentAsync(
            MagazineRootPageId,
            cancellationToken
        );
        if (directIssues.Count > 0)
        {
            return directIssues;
        }

        var roots = await GetPagesBySlugAsync("czasopismo", 1, cancellationToken);
        var rootId = roots.FirstOrDefault()?.Id ?? MagazineRootPageId;
        return await GetPageSummariesByParentAsync(rootId, cancellationToken);
    }

    public async Task<TyfloSwiatIssueDetail> GetIssueAsync(
        int issueId,
        CancellationToken cancellationToken = default
    )
    {
        var pageTask = GetPageAsync(issueId, cancellationToken);
        var childrenTask = GetPageSummariesByParentAsync(issueId, cancellationToken);

        await Task.WhenAll(pageTask, childrenTask);

        var issue = pageTask.Result;
        var children = childrenTask.Result;
        var pdfUrl = TyfloSwiatMagazineParsing.ExtractFirstPdfUrl(issue.Content.Rendered);
        var tocItems = TyfloSwiatMagazineParsing.OrderedTableOfContents(
            children,
            issue.Content.Rendered
        );

        return new TyfloSwiatIssueDetail(issue, pdfUrl, tocItems);
    }

    public async Task<WpPostDetail> GetPageAsync(
        int pageId,
        CancellationToken cancellationToken = default
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyfloswiatApiBaseUrl, $"wp/v2/pages/{pageId}"));
        builder.Query = "_fields=id,date,link,title,excerpt,content,guid";

        return await _cache.GetOrCreateAsync(
            $"wp-tyfloswiat-page:{builder.Uri.AbsoluteUri}",
            IssueDetailCacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var item = await response.Content.ReadFromJsonAsync<WpPostDetail>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return item ?? throw new InvalidOperationException("Brak danych szczegółowych strony.");
            },
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<WpPostSummary>> GetPagesBySlugAsync(
        string slug,
        int perPage,
        CancellationToken cancellationToken
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyfloswiatApiBaseUrl, "wp/v2/pages"));
        builder.Query =
            $"context=embed&per_page={Math.Max(1, perPage)}&slug={Uri.EscapeDataString(slug)}&_fields=id,date,link,title,excerpt";

        return await _cache.GetOrCreateAsync(
            $"wp-tyfloswiat-slug:{builder.Uri.AbsoluteUri}",
            IssuesCacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var items = await response.Content.ReadFromJsonAsync<List<WpPostSummary>>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return (IReadOnlyList<WpPostSummary>)(items ?? []);
            },
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<WpPostSummary>> GetPageSummariesByParentAsync(
        int parentPageId,
        CancellationToken cancellationToken
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyfloswiatApiBaseUrl, "wp/v2/pages"));
        builder.Query =
            $"context=embed&per_page=100&parent={parentPageId}&orderby=date&order=desc&_fields=id,date,link,title,excerpt";

        return await _cache.GetOrCreateAsync(
            $"wp-tyfloswiat-children:{builder.Uri.AbsoluteUri}",
            IssuesCacheTtl,
            async requestCancellationToken =>
            {
                using var response = await _httpClient.GetAsync(
                    builder.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCancellationToken
                );
                response.EnsureSuccessStatusCode();

                var items = await response.Content.ReadFromJsonAsync<List<WpPostSummary>>(
                    SerializerOptions,
                    requestCancellationToken
                );

                return (IReadOnlyList<WpPostSummary>)(items ?? []);
            },
            cancellationToken
        );
    }
}
