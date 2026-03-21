namespace Tyflocentrum.Windows.Domain.Models;

public sealed record TyfloSwiatIssueDetail(
    WpPostDetail Issue,
    string? PdfUrl,
    IReadOnlyList<WpPostSummary> TocItems
);
