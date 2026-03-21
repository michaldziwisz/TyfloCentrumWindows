using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface INewsFeedService
{
    Task<IReadOnlyList<NewsFeedItem>> GetLatestItemsAsync(
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<NewsFeedItem>> GetLatestItemsPageAsync(
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default
    );
}
