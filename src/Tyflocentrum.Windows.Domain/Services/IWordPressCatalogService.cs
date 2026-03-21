using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IWordPressCatalogService
{
    Task<IReadOnlyList<WpCategorySummary>> GetCategoriesAsync(
        ContentSource source,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<WpPostSummary>> GetItemsAsync(
        ContentSource source,
        int pageSize,
        int? categoryId = null,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<WpPostSummary>> GetItemsPageAsync(
        ContentSource source,
        int pageSize,
        int pageNumber,
        int? categoryId = null,
        CancellationToken cancellationToken = default
    );
}
