using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class NewsFeedViewModel : ObservableObject
{
    private const int PageSize = 20;
    private readonly ContentTypeAnnouncementPreferenceService _contentTypeAnnouncementPreferenceService;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly INewsFeedService _newsFeedService;
    private int _currentPageNumber;
    private bool _hasRequestedInitialLoad;
    private bool _hasMoreItems;
    private bool _isRefreshingLatestItems;
    private DateTimeOffset? _lastSuccessfulRefreshAtUtc;

    public NewsFeedViewModel(
        INewsFeedService newsFeedService,
        IExternalLinkLauncher externalLinkLauncher,
        ContentTypeAnnouncementPreferenceService contentTypeAnnouncementPreferenceService
    )
    {
        _newsFeedService = newsFeedService;
        _externalLinkLauncher = externalLinkLauncher;
        _contentTypeAnnouncementPreferenceService = contentTypeAnnouncementPreferenceService;
        _contentTypeAnnouncementPreferenceService.Changed += OnContentTypeAnnouncementPlacementChanged;
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

    public async Task RefreshIfStaleAsync(
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default
    )
    {
        if (!_hasRequestedInitialLoad)
        {
            await LoadIfNeededAsync(cancellationToken);
            return;
        }

        if (!IsRefreshDue(staleAfter))
        {
            return;
        }

        await RefreshLatestItemsAsync(cancellationToken);
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
            var page = await _newsFeedService.GetLatestItemsPageAsync(
                PageSize,
                nextPageNumber,
                cancellationToken
            );
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
            var page = await _newsFeedService.GetLatestItemsPageAsync(
                PageSize,
                1,
                cancellationToken
            );

            Items.Clear();
            AppendItems(page.Items);
            _currentPageNumber = 1;
            _hasMoreItems = page.HasMoreItems;
            _lastSuccessfulRefreshAtUtc = DateTimeOffset.UtcNow;

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
        var existingKeys = new HashSet<(NewsItemKind Kind, int PostId)>(
            Items.Select(item => (item.Kind, item.PostId))
        );

        foreach (var item in items)
        {
            if (!existingKeys.Add((item.Kind, item.Post.Id)))
            {
                continue;
            }

            Items.Add(
                new NewsFeedItemViewModel(item, _contentTypeAnnouncementPreferenceService.Placement)
            );
        }
    }

    private async Task RefreshLatestItemsAsync(CancellationToken cancellationToken)
    {
        if (IsLoading || IsLoadingMore || _isRefreshingLatestItems)
        {
            return;
        }

        _isRefreshingLatestItems = true;

        try
        {
            var page = await _newsFeedService.GetLatestItemsPageAsync(PageSize, 1, cancellationToken);
            PrependNewItems(page.Items);
            _hasMoreItems = _currentPageNumber > 1 || page.HasMoreItems;
            _lastSuccessfulRefreshAtUtc = DateTimeOffset.UtcNow;
            ErrorMessage = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
        finally
        {
            _isRefreshingLatestItems = false;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasMoreItems));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private void PrependNewItems(IEnumerable<NewsFeedItem> items)
    {
        var existingKeys = new HashSet<(NewsItemKind Kind, int PostId)>(
            Items.Select(item => (item.Kind, item.PostId))
        );

        var newItems = items
            .Where(item => existingKeys.Add((item.Kind, item.Post.Id)))
            .Select(item => new NewsFeedItemViewModel(item, _contentTypeAnnouncementPreferenceService.Placement))
            .ToArray();

        for (var index = newItems.Length - 1; index >= 0; index--)
        {
            Items.Insert(0, newItems[index]);
        }
    }

    private bool IsRefreshDue(TimeSpan staleAfter)
    {
        if (staleAfter <= TimeSpan.Zero)
        {
            return true;
        }

        return !_lastSuccessfulRefreshAtUtc.HasValue
            || DateTimeOffset.UtcNow - _lastSuccessfulRefreshAtUtc.Value >= staleAfter;
    }

    private void OnContentTypeAnnouncementPlacementChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.SetContentTypeAnnouncementPlacement(_contentTypeAnnouncementPreferenceService.Placement);
        }
    }
}
