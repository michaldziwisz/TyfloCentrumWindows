using System.Text.Json;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.Infrastructure.Storage;

public sealed class FileFavoritesService : IFavoritesService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private List<FavoriteItem>? _items;

    public FileFavoritesService(string? filePath = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            _filePath = filePath;
            return;
        }

        var rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tyflocentrum.Windows"
        );
        _filePath = Path.Combine(rootPath, "favorites.json");
    }

    public async Task<IReadOnlyList<FavoriteItem>> GetItemsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _items!.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsFavoriteAsync(
        string favoriteId,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _items!.Any(item => string.Equals(item.Id, favoriteId, StringComparison.Ordinal));
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<bool> IsFavoriteAsync(
        ContentSource source,
        int postId,
        FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
        CancellationToken cancellationToken = default
    )
    {
        return IsFavoriteAsync(
            FavoriteItem.CreateId(source, postId, articleOrigin),
            cancellationToken
        );
    }

    public async Task AddOrUpdateAsync(FavoriteItem item, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);

            var id = string.IsNullOrWhiteSpace(item.Id)
                ? FavoriteItem.CreateId(item.Source, item.PostId, item.ResolvedArticleOrigin)
                : item.Id;

            var normalizedItem = item with
            {
                Id = id,
                Kind = item.ResolvedKind,
                ArticleOrigin = item.ResolvedArticleOrigin,
                SavedAtUtc = item.SavedAtUtc == default ? DateTimeOffset.UtcNow : item.SavedAtUtc,
            };

            var existingIndex = _items!.FindIndex(candidate =>
                string.Equals(candidate.Id, id, StringComparison.Ordinal)
            );

            if (existingIndex >= 0)
            {
                var existingSavedAtUtc = _items[existingIndex].SavedAtUtc;
                _items[existingIndex] = normalizedItem with { SavedAtUtc = existingSavedAtUtc };
            }
            else
            {
                _items.Insert(0, normalizedItem);
            }

            _items.Sort((left, right) => right.SavedAtUtc.CompareTo(left.SavedAtUtc));
            await PersistAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(
        string favoriteId,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            _items!.RemoveAll(item => string.Equals(item.Id, favoriteId, StringComparison.Ordinal));
            await PersistAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task RemoveAsync(
        ContentSource source,
        int postId,
        FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
        CancellationToken cancellationToken = default
    )
    {
        return RemoveAsync(FavoriteItem.CreateId(source, postId, articleOrigin), cancellationToken);
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_items is not null)
        {
            return;
        }

        if (!File.Exists(_filePath))
        {
            _items = [];
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _items =
                await JsonSerializer.DeserializeAsync<List<FavoriteItem>>(
                    stream,
                    SerializerOptions,
                    cancellationToken
                ) ?? [];
            NormalizeLoadedItems();
        }
        catch
        {
            _items = [];
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var directoryPath = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, _items, SerializerOptions, cancellationToken);
    }

    private void NormalizeLoadedItems()
    {
        if (_items is null || _items.Count == 0)
        {
            return;
        }

        _items = _items
            .Select(item =>
            {
                var articleOrigin = item.ResolvedArticleOrigin;
                var id = string.IsNullOrWhiteSpace(item.Id)
                    ? FavoriteItem.CreateId(item.Source, item.PostId, articleOrigin)
                    : item.Id;

                return item with
                {
                    Id = id,
                    Kind = item.ResolvedKind,
                    ArticleOrigin = articleOrigin,
                };
            })
            .OrderByDescending(item => item.SavedAtUtc)
            .ToList();
    }
}
