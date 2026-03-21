using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Infrastructure.Storage;
using Tyflocentrum.Windows.UI.Services;
using Xunit;

namespace Tyflocentrum.Windows.Tests.UI;

public sealed class ContentFavoriteServiceTests
{
    [Fact]
    public async Task ToggleAsync_adds_missing_post_to_favorites()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-content-favorites.json");

        try
        {
            var favoritesService = new FileFavoritesService(filePath);
            var service = new ContentFavoriteService(favoritesService);

            var isFavorite = await service.ToggleAsync(
                ContentSource.Podcast,
                101,
                "Podcast testowy",
                "20.03.2026",
                "https://example.invalid/podcast/101"
            );

            var items = await favoritesService.GetItemsAsync();
            var item = Assert.Single(items);
            Assert.True(isFavorite);
            Assert.Equal(FavoriteKind.Podcast, item.ResolvedKind);
            Assert.Equal("Podcast testowy", item.Title);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ToggleAsync_removes_existing_post_from_favorites()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-content-favorites.json");

        try
        {
            var favoritesService = new FileFavoritesService(filePath);
            await favoritesService.AddOrUpdateAsync(new FavoriteItem
            {
                Id = FavoriteItem.CreateId(ContentSource.Article, 202),
                Kind = FavoriteKind.Article,
                Source = ContentSource.Article,
                PostId = 202,
                Title = "Artykuł testowy",
                PublishedDate = "20.03.2026",
                Link = "https://example.invalid/article/202",
            });

            var service = new ContentFavoriteService(favoritesService);

            var isFavorite = await service.ToggleAsync(
                ContentSource.Article,
                202,
                "Artykuł testowy",
                "20.03.2026",
                "https://example.invalid/article/202"
            );

            var items = await favoritesService.GetItemsAsync();
            Assert.False(isFavorite);
            Assert.Empty(items);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Theory]
    [InlineData(true, "Usuń z ulubionych")]
    [InlineData(false, "Dodaj do ulubionych")]
    public void GetToggleLabel_returns_expected_text(bool isFavorite, string expected)
    {
        Assert.Equal(expected, ContentFavoriteService.GetToggleLabel(isFavorite));
    }
}
