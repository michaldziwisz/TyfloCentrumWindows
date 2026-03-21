using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.UI.ViewModels;

namespace Tyflocentrum.Windows.UI.Services;

public sealed class ContentFavoriteService
{
    private readonly IFavoritesService _favoritesService;

    public ContentFavoriteService(IFavoritesService favoritesService)
    {
        _favoritesService = favoritesService;
    }

    public Task<bool> IsFavoriteAsync(
        ContentPostItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsFavoriteAsync(item.Source, item.PostId, cancellationToken);
    }

    public Task<bool> IsFavoriteAsync(
        NewsFeedItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsFavoriteAsync(item.Source, item.PostId, cancellationToken);
    }

    public Task<bool> IsFavoriteAsync(
        ContentSource source,
        int postId,
        CancellationToken cancellationToken = default
    )
    {
        return _favoritesService.IsFavoriteAsync(
            source,
            postId,
            FavoriteArticleOrigin.Post,
            cancellationToken
        );
    }

    public Task<bool> ToggleAsync(
        ContentPostItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);
        return ToggleAsync(
            item.Source,
            item.PostId,
            item.Title,
            item.PublishedDate,
            item.Link,
            cancellationToken
        );
    }

    public Task<bool> ToggleAsync(
        NewsFeedItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);
        return ToggleAsync(
            item.Source,
            item.PostId,
            item.Title,
            item.PublishedDate,
            item.Link,
            cancellationToken
        );
    }

    public async Task<bool> ToggleAsync(
        ContentSource source,
        int postId,
        string title,
        string publishedDate,
        string link,
        CancellationToken cancellationToken = default
    )
    {
        var isFavorite = await IsFavoriteAsync(source, postId, cancellationToken);
        if (isFavorite)
        {
            await _favoritesService.RemoveAsync(
                source,
                postId,
                FavoriteArticleOrigin.Post,
                cancellationToken
            );
            return false;
        }

        await _favoritesService.AddOrUpdateAsync(
            CreateFavoriteItem(source, postId, title, publishedDate, link),
            cancellationToken
        );
        return true;
    }

    public static string GetToggleLabel(bool isFavorite)
    {
        return isFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych";
    }

    private static FavoriteItem CreateFavoriteItem(
        ContentSource source,
        int postId,
        string title,
        string publishedDate,
        string link
    )
    {
        return new FavoriteItem
        {
            Id = FavoriteItem.CreateId(source, postId),
            Kind = source == ContentSource.Podcast ? FavoriteKind.Podcast : FavoriteKind.Article,
            ArticleOrigin = FavoriteArticleOrigin.Post,
            Source = source,
            PostId = postId,
            Title = title,
            PublishedDate = publishedDate,
            Link = link,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
