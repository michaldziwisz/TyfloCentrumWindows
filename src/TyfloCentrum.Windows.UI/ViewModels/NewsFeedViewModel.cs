using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class NewsFeedViewModel : ObservableObject
{
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly INewsFeedService _newsFeedService;
    private int _currentPageNumber;
    private bool _hasRequestedInitialLoad;
    private bool _hasMoreItems;

    public NewsFeedViewModel(
        INewsFeedService newsFeedService,
        IExternalLinkLauncher externalLinkLauncher
    )
    {
        _newsFeedService = newsFeedService;
        _externalLinkLauncher = externalLinkLauncher;
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => !IsLoading);
    }

    public ObservableCollection<NewsFeedItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isLoadingMore;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasItems => Items.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowEmptyState => HasLoaded && !IsLoading && !HasItems && !HasError;

    public bool HasMoreItems => _hasMoreItems;

    public IAsyncRelayCommand RetryCommand { get; }

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasRequestedInitialLoad)
        {
            return;
        }

        _hasRequestedInitialLoad = true;
        await LoadAsync(cancellationToken);
    }

    public async Task RetryAsync()
    {
        await LoadAsync();
    }

    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading || IsLoadingMore || !HasMoreItems)
        {
            return;
        }

        IsLoadingMore = true;

        try
        {
            var nextPageNumber = _currentPageNumber + 1;
            var page = await _newsFeedService.GetLatestItemsPageAsync(20, nextPageNumber, cancellationToken);
            AppendItems(page.Items);
            _currentPageNumber = nextPageNumber;
            _hasMoreItems = page.HasMoreItems;
            ErrorMessage = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ErrorMessage = "Nie udało się wczytać starszych treści. Spróbuj przewinąć ponownie.";
        }
        finally
        {
            IsLoadingMore = false;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasMoreItems));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public async Task OpenItemAsync(NewsFeedItemViewModel item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.Link))
        {
            ErrorMessage = "Ten wpis nie ma poprawnego linku do otwarcia.";
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(item.Link, cancellationToken);
        if (!launched)
        {
            ErrorMessage = "Nie udało się otworzyć linku w przeglądarce.";
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        IsLoadingMore = false;
        ErrorMessage = null;

        try
        {
            var page = await _newsFeedService.GetLatestItemsPageAsync(20, 1, cancellationToken);

            Items.Clear();
            AppendItems(page.Items);
            _currentPageNumber = 1;
            _hasMoreItems = page.HasMoreItems;

            HasLoaded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Items.Clear();
            _currentPageNumber = 0;
            _hasMoreItems = false;
            ErrorMessage = "Nie udało się pobrać danych. Spróbuj ponownie.";
            HasLoaded = true;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasMoreItems));
            OnPropertyChanged(nameof(ShowEmptyState));
            RetryCommand.NotifyCanExecuteChanged();
        }
    }

    private void AppendItems(IEnumerable<NewsFeedItem> items)
    {
        foreach (var item in items)
        {
            Items.Add(new NewsFeedItemViewModel(item));
        }
    }
}
