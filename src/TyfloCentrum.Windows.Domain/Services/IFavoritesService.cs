using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IFavoritesService
{
    Task<IReadOnlyList<FavoriteItem>> GetItemsAsync(CancellationToken cancellationToken = default);

    Task<bool> IsFavoriteAsync(string favoriteId, CancellationToken cancellationToken = default);

    Task<bool> IsFavoriteAsync(
        ContentSource source,
        int postId,
        FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
        CancellationToken cancellationToken = default
    );

    Task AddOrUpdateAsync(FavoriteItem item, CancellationToken cancellationToken = default);

    Task RemoveAsync(string favoriteId, CancellationToken cancellationToken = default);

    Task RemoveAsync(
        ContentSource source,
        int postId,
        FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
        CancellationToken cancellationToken = default
    );
}
