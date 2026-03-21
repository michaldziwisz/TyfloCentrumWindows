using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IWordPressSearchService
{
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        SearchScope scope,
        string query,
        int pageSize,
        CancellationToken cancellationToken = default
    );
}
