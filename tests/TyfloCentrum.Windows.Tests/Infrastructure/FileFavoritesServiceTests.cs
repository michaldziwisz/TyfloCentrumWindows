using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class FileFavoritesServiceTests
{
    [Fact]
    public async Task AddOrUpdateAsync_persists_items_and_updates_existing_entry_without_duplicates()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-favorites.json");

        try
        {
            var service = new FileFavoritesService(filePath);
            await service.AddOrUpdateAsync(new FavoriteItem
            {
                Id = FavoriteItem.CreateId(ContentSource.Podcast, 11),
                Kind = FavoriteKind.Podcast,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Podcast 11",
                PublishedDate = "19.03.2026",
                Link = "https://example.invalid/podcast/11",
                SavedAtUtc = new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero),
            });
            await service.AddOrUpdateAsync(new FavoriteItem
            {
                Id = FavoriteItem.CreateId(ContentSource.Article, 22),
                Kind = FavoriteKind.Article,
                Source = ContentSource.Article,
                PostId = 22,
                Title = "Artykuł 22",
                PublishedDate = "20.03.2026",
                Link = "https://example.invalid/article/22",
                SavedAtUtc = new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero),
            });
            await service.AddOrUpdateAsync(new FavoriteItem
            {
                Id = FavoriteItem.CreateId(ContentSource.Podcast, 11),
                Kind = FavoriteKind.Podcast,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Podcast 11 poprawiony",
                PublishedDate = "19.03.2026",
                Link = "https://example.invalid/podcast/11",
                SavedAtUtc = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });

            var reloadedService = new FileFavoritesService(filePath);
            var items = await reloadedService.GetItemsAsync();

            Assert.Collection(
                items,
                item =>
                {
                    Assert.Equal(ContentSource.Article, item.Source);
                    Assert.Equal("Artykuł 22", item.Title);
                },
                item =>
                {
                    Assert.Equal(ContentSource.Podcast, item.Source);
                    Assert.Equal("Podcast 11 poprawiony", item.Title);
                    Assert.Equal(
                        new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero),
                        item.SavedAtUtc
                    );
                }
            );
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
    public async Task RemoveAsync_deletes_item_from_persisted_store()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-favorites.json");

        try
        {
            var service = new FileFavoritesService(filePath);
            await service.AddOrUpdateAsync(new FavoriteItem
            {
                Id = FavoriteItem.CreateId(ContentSource.Podcast, 11),
                Kind = FavoriteKind.Podcast,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Podcast 11",
                PublishedDate = "19.03.2026",
                Link = "https://example.invalid/podcast/11",
                SavedAtUtc = new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero),
            });

            await service.RemoveAsync(ContentSource.Podcast, 11);

            var reloadedService = new FileFavoritesService(filePath);
            var items = await reloadedService.GetItemsAsync();

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

    [Fact]
    public async Task RemoveAsync_by_id_deletes_only_selected_topic_for_same_podcast()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-favorites.json");

        try
        {
            var service = new FileFavoritesService(filePath);
            await service.AddOrUpdateAsync(new FavoriteItem
            {
                Id = FavoriteItem.CreateTopicId(11, "Temat 1", 123),
                Kind = FavoriteKind.Topic,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Temat 1",
                Subtitle = "Podcast 11",
                PublishedDate = "19.03.2026",
                StartPositionSeconds = 123,
                SavedAtUtc = new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero),
            });
            await service.AddOrUpdateAsync(new FavoriteItem
            {
                Id = FavoriteItem.CreateTopicId(11, "Temat 2", 456),
                Kind = FavoriteKind.Topic,
                Source = ContentSource.Podcast,
                PostId = 11,
                Title = "Temat 2",
                Subtitle = "Podcast 11",
                PublishedDate = "19.03.2026",
                StartPositionSeconds = 456,
                SavedAtUtc = new DateTimeOffset(2026, 3, 19, 12, 5, 0, TimeSpan.Zero),
            });

            await service.RemoveAsync(FavoriteItem.CreateTopicId(11, "Temat 1", 123));

            var reloadedService = new FileFavoritesService(filePath);
            var items = await reloadedService.GetItemsAsync();

            var item = Assert.Single(items);
            Assert.Equal("Temat 2", item.Title);
            Assert.Equal(FavoriteKind.Topic, item.ResolvedKind);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
