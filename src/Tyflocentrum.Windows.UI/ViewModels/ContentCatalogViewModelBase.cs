using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.UI.ViewModels;

public abstract partial class ContentCatalogViewModelBase : ObservableObject
{
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IWordPressCatalogService _catalogService;
    private readonly ContentSource _source;
    private int _currentPageNumber;
    private bool _hasRequestedInitialLoad;
    private bool _hasMoreItems;
    private bool _reloadInProgress;
    private bool _reloadQueued;
    private bool _queuedIncludeCategories;

    protected ContentCatalogViewModelBase(
        ContentSource source,
        IWordPressCatalogService catalogService,
        IExternalLinkLauncher externalLinkLauncher
    )
    {
        _source = source;
        _catalogService = catalogService;
        _externalLinkLauncher = externalLinkLauncher;
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => !IsLoading);
    }

    public ObservableCollection<ContentCategoryItemViewModel> Categories { get; } = [];

    public ObservableCollection<ContentPostItemViewModel> Items { get; } = [];

    public abstract string LeadText { get; }

    public abstract string ListAutomationName { get; }

    public abstract string CategoriesAutomationName { get; }

    protected abstract string EmptyStateMessage { get; }

    protected abstract string LoadErrorMessage { get; }

    protected abstract string OpenErrorMessage { get; }

    protected abstract string AllCategoriesLabel { get; }

    protected abstract string AllItemsHeading { get; }

    protected abstract string CategoryItemsHeadingFormat { get; }

    protected int PageSize => 20;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isLoadingMore;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private ContentCategoryItemViewModel? selectedCategory;

    public bool HasItems => Items.Count > 0;

    public bool HasCategories => Categories.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowEmptyState => HasLoaded && !IsLoading && !HasItems && !HasError;

    public bool HasMoreItems => _hasMoreItems;

    public string CurrentItemsHeading =>
        SelectedCategory?.Id is null
            ? AllItemsHeading
            : string.Format(CategoryItemsHeadingFormat, SelectedCategory.Name);

    public string EmptyStateText => EmptyStateMessage;

    public IAsyncRelayCommand RetryCommand { get; }

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasRequestedInitialLoad)
        {
            return;
        }

        _hasRequestedInitialLoad = true;
        await ReloadAsync(includeCategories: true, cancellationToken);
    }

    public async Task RetryAsync()
    {
        await ReloadAsync(includeCategories: true);
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
            var page = await _catalogService.GetItemsPageAsync(
                _source,
                PageSize,
                nextPageNumber,
                SelectedCategory?.Id,
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
            NotifyCollectionStateChanged();
        }
    }

    public async Task SelectCategoryAsync(
        ContentCategoryItemViewModel? category,
        CancellationToken cancellationToken = default
    )
    {
        var nextCategory = category ?? Categories.FirstOrDefault();
        if (nextCategory is null)
        {
            return;
        }

        if (ReferenceEquals(SelectedCategory, nextCategory))
        {
            return;
        }

        SelectedCategory = nextCategory;
        OnPropertyChanged(nameof(CurrentItemsHeading));
        await ReloadAsync(includeCategories: false, cancellationToken);
    }

    public async Task OpenItemAsync(
        ContentPostItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(item.Link))
        {
            ErrorMessage = OpenErrorMessage;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(item.Link, cancellationToken);
        if (!launched)
        {
            ErrorMessage = OpenErrorMessage;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private async Task ReloadAsync(bool includeCategories, CancellationToken cancellationToken = default)
    {
        if (_reloadInProgress)
        {
            _reloadQueued = true;
            _queuedIncludeCategories |= includeCategories;
            return;
        }

        do
        {
            _reloadInProgress = true;
            var currentIncludeCategories = includeCategories;
            _reloadQueued = false;
            _queuedIncludeCategories = false;

            IsLoading = true;
            IsLoadingMore = false;
            ErrorMessage = null;

            try
            {
                if (currentIncludeCategories || !HasCategories)
                {
                    await LoadCategoriesAsync(cancellationToken);
                }

                await LoadItemsAsync(cancellationToken);
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
                ErrorMessage = LoadErrorMessage;
                HasLoaded = true;
            }
            finally
            {
                IsLoading = false;
                _reloadInProgress = false;
                NotifyCollectionStateChanged();
            }

            includeCategories = _queuedIncludeCategories;
        } while (_reloadQueued && !cancellationToken.IsCancellationRequested);
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var fetchedCategories = await _catalogService.GetCategoriesAsync(_source, cancellationToken);
        var previousSelectedCategoryId = SelectedCategory?.Id;

        Categories.Clear();
        Categories.Add(new ContentCategoryItemViewModel(null, AllCategoriesLabel));

        foreach (var category in fetchedCategories)
        {
            Categories.Add(new ContentCategoryItemViewModel(category.Id, category.Name, category.Count));
        }

        SelectedCategory =
            Categories.FirstOrDefault(item => item.Id == previousSelectedCategoryId)
            ?? Categories.FirstOrDefault();

        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(CurrentItemsHeading));
    }

    private async Task LoadItemsAsync(CancellationToken cancellationToken)
    {
        var page = await _catalogService.GetItemsPageAsync(
            _source,
            PageSize,
            1,
            SelectedCategory?.Id,
            cancellationToken
        );

        Items.Clear();
        AppendItems(page.Items);
        _currentPageNumber = 1;
        _hasMoreItems = page.HasMoreItems;

        NotifyCollectionStateChanged();
        OnPropertyChanged(nameof(CurrentItemsHeading));
    }

    private void AppendItems(IEnumerable<WpPostSummary> items)
    {
        foreach (var item in items)
        {
            Items.Add(new ContentPostItemViewModel(_source, item));
        }
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasMoreItems));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CurrentItemsHeading));
        RetryCommand.NotifyCanExecuteChanged();
    }
}
