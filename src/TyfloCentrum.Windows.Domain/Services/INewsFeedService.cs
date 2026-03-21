using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

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
