using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface ITyfloSwiatMagazineService
{
    Task<IReadOnlyList<WpPostSummary>> GetIssuesAsync(CancellationToken cancellationToken = default);

    Task<TyfloSwiatIssueDetail> GetIssueAsync(
        int issueId,
        CancellationToken cancellationToken = default
    );

    Task<WpPostDetail> GetPageAsync(int pageId, CancellationToken cancellationToken = default);
}
