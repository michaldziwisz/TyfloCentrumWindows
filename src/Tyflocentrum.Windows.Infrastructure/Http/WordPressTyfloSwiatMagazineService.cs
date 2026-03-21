using System.Net.Http.Json;
using System.Text.Json;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.Domain.Text;

namespace Tyflocentrum.Windows.Infrastructure.Http;

public sealed class WordPressTyfloSwiatMagazineService : ITyfloSwiatMagazineService
{
    private const int MagazineRootPageId = 1409;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TyflocentrumEndpointsOptions _options;

    public WordPressTyfloSwiatMagazineService(
        HttpClient httpClient,
        TyflocentrumEndpointsOptions options
    )
    {
        _httpClient = httpClient;
        _options = options;
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

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var item = await response.Content.ReadFromJsonAsync<WpPostDetail>(
            SerializerOptions,
            cancellationToken
        );

        return item ?? throw new InvalidOperationException("Brak danych szczegółowych strony.");
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

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<WpPostSummary>>(
            SerializerOptions,
            cancellationToken
        );

        return items ?? [];
    }

    private async Task<IReadOnlyList<WpPostSummary>> GetPageSummariesByParentAsync(
        int parentPageId,
        CancellationToken cancellationToken
    )
    {
        var builder = new UriBuilder(new Uri(_options.TyfloswiatApiBaseUrl, "wp/v2/pages"));
        builder.Query =
            $"context=embed&per_page=100&parent={parentPageId}&orderby=date&order=desc&_fields=id,date,link,title,excerpt";

        using var response = await _httpClient.GetAsync(
            builder.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<WpPostSummary>>(
            SerializerOptions,
            cancellationToken
        );

        return items ?? [];
    }
}
