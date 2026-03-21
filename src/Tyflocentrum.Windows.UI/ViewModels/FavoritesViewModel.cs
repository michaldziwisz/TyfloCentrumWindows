using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.UI.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFavoritesService _favoritesService;
    private readonly IShareService _shareService;
    private bool _hasLoaded;

    public FavoritesViewModel(
        IFavoritesService favoritesService,
        IExternalLinkLauncher externalLinkLauncher,
        IClipboardService clipboardService,
        IShareService shareService
    )
    {
        _favoritesService = favoritesService;
        _externalLinkLauncher = externalLinkLauncher;
        _clipboardService = clipboardService;
        _shareService = shareService;

        Filters.Add(new FavoriteFilterOptionViewModel("all", "Wszystko", null));
        Filters.Add(new FavoriteFilterOptionViewModel("podcasts", "Podcasty", FavoriteKind.Podcast));
        Filters.Add(new FavoriteFilterOptionViewModel("articles", "Artykuły", FavoriteKind.Article));
        Filters.Add(new FavoriteFilterOptionViewModel("topics", "Tematy", FavoriteKind.Topic));
        Filters.Add(new FavoriteFilterOptionViewModel("links", "Linki", FavoriteKind.Link));
        SelectedFilter = Filters[0];
    }

    public ObservableCollection<FavoriteFilterOptionViewModel> Filters { get; } = [];

    public ObservableCollection<FavoriteItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private FavoriteFilterOptionViewModel? selectedFilter;

    public bool HasItems => Items.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowEmptyState => HasLoadedOnce && !IsLoading && !HasItems && !HasError;

    public string EmptyStateText =>
        SelectedFilter?.Kind switch
        {
            FavoriteKind.Podcast => "Nie masz jeszcze ulubionych podcastów.",
            FavoriteKind.Article => "Nie masz jeszcze ulubionych artykułów.",
            FavoriteKind.Topic => "Nie masz jeszcze ulubionych tematów.",
            FavoriteKind.Link => "Nie masz jeszcze ulubionych linków.",
            _ => "Nie masz jeszcze żadnych ulubionych pozycji.",
        };

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await ReloadAsync(cancellationToken);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        _hasLoaded = true;
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Ładowanie ulubionych…";
        NotifyStateChanged();

        try
        {
            var items = await _favoritesService.GetItemsAsync(cancellationToken);
            ApplyItems(items);
            HasLoadedOnce = true;
            StatusMessage = BuildStatusMessage(Items.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Items.Clear();
            ErrorMessage = "Nie udało się wczytać ulubionych. Spróbuj ponownie.";
            StatusMessage = ErrorMessage;
            HasLoadedOnce = true;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task SelectFilterAsync(
        FavoriteFilterOptionViewModel? filter,
        CancellationToken cancellationToken = default
    )
    {
        SelectedFilter = filter ?? Filters.FirstOrDefault() ?? SelectedFilter;
        await ReloadAsync(cancellationToken);
    }

    public async Task<bool> OpenItemAsync(
        FavoriteItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(item.Link))
        {
            ErrorMessage = "Ta pozycja nie ma poprawnego linku do otwarcia.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(item.Link, cancellationToken);
        if (!launched)
        {
            ErrorMessage = "Nie udało się otworzyć ulubionej pozycji w przeglądarce.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        return true;
    }

    public async Task<bool> CopyLinkAsync(
        FavoriteItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(item.Link))
        {
            ErrorMessage = "Ta pozycja nie ma poprawnego odnośnika do skopiowania.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        var copied = await _clipboardService.SetTextAsync(item.Link, cancellationToken);
        if (!copied)
        {
            ErrorMessage = "Nie udało się skopiować odnośnika.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        ErrorMessage = null;
        StatusMessage = $"Skopiowano odnośnik: {item.Title}.";
        NotifyStateChanged();
        return true;
    }

    public async Task<bool> ShareItemAsync(
        FavoriteItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(item.Link))
        {
            ErrorMessage = "Ta pozycja nie ma poprawnego odnośnika do udostępnienia.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        var shared = await _shareService.ShareLinkAsync(
            item.Title,
            item.ContextTitle,
            item.Link,
            cancellationToken
        );
        if (!shared)
        {
            ErrorMessage = "Nie udało się udostępnić odnośnika.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        ErrorMessage = null;
        StatusMessage = $"Otwarto systemowe udostępnianie dla: {item.Title}.";
        NotifyStateChanged();
        return true;
    }

    public async Task RemoveAsync(
        FavoriteItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        await _favoritesService.RemoveAsync(item.Id, cancellationToken);
        await ReloadAsync(cancellationToken);
        StatusMessage = "Pozycja została usunięta z ulubionych.";
        NotifyStateChanged();
    }

    private void ApplyItems(IReadOnlyList<FavoriteItem> allItems)
    {
        var filteredItems = SelectedFilter?.Kind is null
            ? allItems
            : allItems.Where(item => item.ResolvedKind == SelectedFilter.Kind).ToArray();

        Items.Clear();
        foreach (var item in filteredItems)
        {
            Items.Add(new FavoriteItemViewModel(item));
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyStateText));
    }

    private static string BuildStatusMessage(int count)
    {
        return count switch
        {
            0 => "Brak ulubionych pozycji.",
            1 => "Masz 1 ulubioną pozycję.",
            >= 2 and <= 4 => $"Masz {count} ulubione pozycje.",
            _ => $"Masz {count} ulubionych pozycji.",
        };
    }
}
