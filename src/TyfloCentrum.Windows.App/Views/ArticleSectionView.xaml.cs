using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class ArticleSectionView : UserControl
{
    private readonly ContentEntryActionService _contentEntryActionService;
    private readonly ContentFavoriteService _contentFavoriteService;
    private readonly IShareService _shareService;
    private readonly PostDetailDialogService _postDetailDialogService;
    private readonly TyfloSwiatMagazineView _magazineView;
    private readonly ContentCategoryItemViewModel _magazineNavigationItem = new(
        -1,
        "Czasopismo Tyfloświat"
    );
    private readonly ObservableCollection<ContentCategoryItemViewModel> _navigationItems = [];
    private ContentCategoryItemViewModel? _pendingFocusedNavigationItem;
    private bool _focusSelectedContentWhenReady;
    private bool _isMagazineSelected;
    private bool _restoreFocusToCategoriesList;
    private bool _synchronizingCategorySelection;

    public event EventHandler? ExitToSectionListRequested;

    public ArticleSectionView(
        ArticleCatalogViewModel viewModel,
        ContentEntryActionService contentEntryActionService,
        ContentFavoriteService contentFavoriteService,
        IShareService shareService,
        PostDetailDialogService postDetailDialogService,
        TyfloSwiatMagazineView magazineView
    )
    {
        ViewModel = viewModel;
        _contentEntryActionService = contentEntryActionService;
        _contentFavoriteService = contentFavoriteService;
        _shareService = shareService;
        _postDetailDialogService = postDetailDialogService;
        _magazineView = magazineView;
        InitializeComponent();
        DataContext = ViewModel;
        CategoriesList.ItemsSource = _navigationItems;
        MagazineContentHost.Content = _magazineView;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Categories.CollectionChanged += OnCategoriesCollectionChanged;
        ViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
        RebuildNavigationItems();
        UpdateVisualState();
    }

    public ArticleCatalogViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        if (_navigationItems.Count > 0)
        {
            ListViewFocusHelper.RestoreFocus(CategoriesList, _navigationItems[0]);
            return;
        }

        if (!_isMagazineSelected && ViewModel.HasItems && ViewModel.Items.Count > 0)
        {
            ListViewFocusHelper.RestoreFocus(ItemsList, ViewModel.Items[0]);
            return;
        }

        CategoriesList.Focus(FocusState.Programmatic);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadIfNeededAsync();
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryAsync();
    }

    private async void OnCategoriesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingCategorySelection)
        {
            return;
        }

        if (sender is ListView { SelectedItem: ContentCategoryItemViewModel navigationItem })
        {
            _pendingFocusedNavigationItem = navigationItem;
            _restoreFocusToCategoriesList = FocusNavigationHelper.IsFocusWithin(CategoriesList);
            await SelectNavigationItemFromUiAsync(navigationItem);
            RestorePendingNavigationFocus();
        }
    }

    private async void OnCategoriesListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        if (TryGetLatinLetter(e.Key, out var input))
        {
            var categoryItems = ViewModel.Categories;
            if (_navigationItems.Count == 0)
            {
                return;
            }

            var currentCategoryIndex = Math.Max(0, _navigationItems.IndexOf(GetSelectedNavigationItem()));
            var matchedCategory = InitialListNavigationHelper.FindNextByInitial(
                _navigationItems,
                currentCategoryIndex,
                category => category.Name,
                input
            );

            if (matchedCategory is not null)
            {
                e.Handled = true;
                _pendingFocusedNavigationItem = matchedCategory;
                _restoreFocusToCategoriesList = FocusNavigationHelper.IsFocusWithin(CategoriesList);
                _synchronizingCategorySelection = true;
                listView.SelectedItem = matchedCategory;
                _synchronizingCategorySelection = false;
                await SelectNavigationItemFromUiAsync(matchedCategory);
                RestorePendingNavigationFocus();
            }

            return;
        }

        if (e.Key is not (VirtualKey.Up or VirtualKey.Down))
        {
            return;
        }

        if (_navigationItems.Count == 0)
        {
            return;
        }

        var currentIndex = Math.Max(0, _navigationItems.IndexOf(GetSelectedNavigationItem()));
        var nextIndex = e.Key == VirtualKey.Down ? currentIndex + 1 : currentIndex - 1;
        if (nextIndex < 0 || nextIndex >= _navigationItems.Count)
        {
            return;
        }

        var nextCategory = _navigationItems[nextIndex];
        e.Handled = true;
        _pendingFocusedNavigationItem = nextCategory;
        _restoreFocusToCategoriesList = FocusNavigationHelper.IsFocusWithin(CategoriesList);
        _synchronizingCategorySelection = true;
        listView.SelectedItem = nextCategory;
        _synchronizingCategorySelection = false;
        await SelectNavigationItemFromUiAsync(nextCategory);
        RestorePendingNavigationFocus();

    }

    private async void OnCategoriesListPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || sender is not ListView listView)
        {
            return;
        }

        e.Handled = true;

        var selectedNavigationItem =
            listView.SelectedItem as ContentCategoryItemViewModel ?? GetSelectedNavigationItem();

        if (selectedNavigationItem is not null)
        {
            await SelectNavigationItemFromUiAsync(selectedNavigationItem);
        }

        if (
            (!_isMagazineSelected && ViewModel.IsLoading)
            || (_isMagazineSelected && _magazineView.ViewModel.IsLoading)
        )
        {
            _focusSelectedContentWhenReady = true;
            return;
        }

        FocusSelectedContent();
    }

    private async void OnItemsListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ContentPostItemViewModel item)
        {
            await OpenDefaultActionAsync(item);
        }
    }

    private async void OnItemsListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (
            e.Key == VirtualKey.D
            && KeyboardShortcutHelper.IsControlPressed()
            && sender is ListView { SelectedItem: ContentPostItemViewModel favoriteItem }
        )
        {
            e.Handled = true;
            await ToggleFavoriteAsync(favoriteItem);
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (sender is ListView { SelectedItem: ContentPostItemViewModel item })
        {
            e.Handled = true;
            await OpenDefaultActionAsync(item);
        }
    }

    private async void OnItemsListContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args
    )
    {
        if (args.InRecycleQueue)
        {
            return;
        }

        if (args.ItemIndex >= ViewModel.Items.Count - 5)
        {
            await ViewModel.LoadMoreAsync();
        }
    }

    private async void OnItemsListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var item =
            ItemContextResolver.Resolve<ContentPostItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as ContentPostItemViewModel;

        if (item is null)
        {
            return;
        }

        e.Handled = true;
        var isFavorite = await _contentFavoriteService.IsFavoriteAsync(item);

        var flyout = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Otwórz artykuł" };
        AutomationProperties.SetName(openItem, item.DefaultActionLabel);
        openItem.Click += async (_, _) => await OpenDefaultActionAsync(item);
        flyout.Items.Add(openItem);

        var detailsItem = new MenuFlyoutItem { Text = "Szczegóły" };
        AutomationProperties.SetName(detailsItem, item.OpenDetailsLabel);
        detailsItem.Click += async (_, _) => await OpenDetailsAsync(item);
        flyout.Items.Add(detailsItem);

        var browserItem = new MenuFlyoutItem { Text = "Otwórz w przeglądarce" };
        AutomationProperties.SetName(browserItem, item.OpenLinkLabel);
        browserItem.Click += async (_, _) => await ViewModel.OpenItemAsync(item);
        flyout.Items.Add(browserItem);

        var shareItem = new MenuFlyoutItem { Text = "Udostępnij" };
        AutomationProperties.SetName(shareItem, $"Udostępnij artykuł: {item.Title}");
        shareItem.Click += async (_, _) => await ShareItemAsync(item);
        flyout.Items.Add(shareItem);

        var favoriteItem = new MenuFlyoutItem
        {
            Text = ContentFavoriteService.GetToggleLabel(isFavorite),
        };
        AutomationProperties.SetName(
            favoriteItem,
            $"{ContentFavoriteService.GetToggleLabel(isFavorite)}: {item.Title}"
        );
        favoriteItem.Click += async (_, _) => await ToggleFavoriteAsync(item);
        flyout.Items.Add(favoriteItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private async Task OpenDefaultActionAsync(ContentPostItemViewModel item)
    {
        AutomationAnnouncementHelper.Announce(
            ItemsList,
            $"Otwieranie artykułu: {item.Title}.",
            important: true
        );
        var shown = await _contentEntryActionService.OpenDefaultAsync(item, XamlRoot);
        if (!shown)
        {
            await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się otworzyć artykułu w aplikacji.");
        }

        ListViewFocusHelper.RestoreFocus(ItemsList, item);
    }

    private Task OpenDetailsAsync(ContentPostItemViewModel item)
    {
        return _postDetailDialogService.ShowAsync(
            item.Source,
            item.PostId,
            item.Title,
            item.PublishedDate,
            item.Link,
            XamlRoot
        );
    }

    private async Task ShareItemAsync(ContentPostItemViewModel item)
    {
        var shared = await _shareService.ShareLinkAsync(item.Title, item.Excerpt, item.Link);
        if (!shared)
        {
            await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się udostępnić artykułu.");
        }

        ListViewFocusHelper.RestoreFocus(ItemsList, item);
    }

    private async Task ToggleFavoriteAsync(ContentPostItemViewModel item)
    {
        try
        {
            var isFavorite = await _contentFavoriteService.ToggleAsync(item);
            AutomationAnnouncementHelper.Announce(
                ItemsList,
                isFavorite
                    ? $"Dodano do ulubionych: {item.Title}."
                    : $"Usunięto z ulubionych: {item.Title}.",
                important: true
            );
            ListViewFocusHelper.RestoreFocus(ItemsList, item);
        }
        catch
        {
            await DialogHelpers.ShowErrorAsync(
                XamlRoot,
                "Nie udało się zaktualizować ulubionego wpisu."
            );
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void OnCategoriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildNavigationItems();
        UpdateVisualState();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        LoadingIndicator.IsActive = ViewModel.IsLoading;
        LoadingIndicator.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;

        ErrorPanel.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.IsOpen = ViewModel.HasError;
        ErrorBar.Message = ViewModel.ErrorMessage;

        EmptyStateText.Visibility = !_isMagazineSelected && ViewModel.ShowEmptyState
            ? Visibility.Visible
            : Visibility.Collapsed;
        ItemsList.Visibility = !_isMagazineSelected && ViewModel.HasItems
            ? Visibility.Visible
            : Visibility.Collapsed;
        CategoriesList.Visibility = _navigationItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LoadMoreIndicator.IsActive = ViewModel.IsLoadingMore;
        LoadMoreIndicator.Visibility = !_isMagazineSelected && ViewModel.IsLoadingMore
            ? Visibility.Visible
            : Visibility.Collapsed;
        ArticleItemsPanel.Visibility = _isMagazineSelected ? Visibility.Collapsed : Visibility.Visible;
        MagazineContentPanel.Visibility = _isMagazineSelected ? Visibility.Visible : Visibility.Collapsed;

        var selectedNavigationItem = GetSelectedNavigationItem();
        if (!ReferenceEquals(CategoriesList.SelectedItem, selectedNavigationItem))
        {
            _synchronizingCategorySelection = true;
            CategoriesList.SelectedItem = selectedNavigationItem;
            _synchronizingCategorySelection = false;
        }

        RestorePendingNavigationFocus();

        if (
            _focusSelectedContentWhenReady
            && !ViewModel.IsLoading
            && (!_isMagazineSelected || !_magazineView.ViewModel.IsLoading)
        )
        {
            _focusSelectedContentWhenReady = false;
            FocusSelectedContent();
        }
    }

    private async Task SelectNavigationItemFromUiAsync(ContentCategoryItemViewModel navigationItem)
    {
        if (ReferenceEquals(navigationItem, _magazineNavigationItem))
        {
            if (_isMagazineSelected)
            {
                RestorePendingNavigationFocus();
                return;
            }

            _isMagazineSelected = true;
            await _magazineView.ViewModel.RefreshAsync();
            UpdateVisualState();
            return;
        }

        _isMagazineSelected = false;
        var category = navigationItem;
        if (ReferenceEquals(category, ViewModel.SelectedCategory))
        {
            UpdateVisualState();
            RestorePendingNavigationFocus();
            return;
        }

        await ViewModel.SelectCategoryAsync(category);
    }

    private void RestorePendingNavigationFocus()
    {
        if ((!_isMagazineSelected && ViewModel.IsLoading) || _pendingFocusedNavigationItem is null)
        {
            return;
        }

        if (!ReferenceEquals(_pendingFocusedNavigationItem, GetSelectedNavigationItem()))
        {
            _pendingFocusedNavigationItem = null;
            _restoreFocusToCategoriesList = false;
            return;
        }

        CategoriesList.SelectedItem = _pendingFocusedNavigationItem;
        CategoriesList.ScrollIntoView(_pendingFocusedNavigationItem);
        CategoriesList.UpdateLayout();

        _pendingFocusedNavigationItem = null;
        _restoreFocusToCategoriesList = false;
    }

    private void FocusSelectedContent()
    {
        if (_isMagazineSelected)
        {
            _magazineView.FocusPrimaryContent();
            return;
        }

        if (ViewModel.HasItems && ViewModel.Items.Count > 0)
        {
            var selectedItem = ItemsList.SelectedItem as ContentPostItemViewModel ?? ViewModel.Items[0];
            ListViewFocusHelper.RestoreFocus(ItemsList, selectedItem);
            return;
        }

        ItemsList.Focus(FocusState.Programmatic);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        if (FocusNavigationHelper.IsFocusWithin(ItemsList))
        {
            e.Handled = true;
            CategoriesList.Focus(FocusState.Programmatic);
            return;
        }

        if (_isMagazineSelected && FocusNavigationHelper.IsFocusWithin(_magazineView))
        {
            e.Handled = true;
            if (_magazineView.HandleEscapeNavigation())
            {
                return;
            }

            CategoriesList.Focus(FocusState.Programmatic);
            return;
        }

        if (FocusNavigationHelper.IsFocusWithin(CategoriesList) || FocusNavigationHelper.IsFocusWithin(this))
        {
            e.Handled = true;
            ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private ContentCategoryItemViewModel GetSelectedNavigationItem()
    {
        if (_isMagazineSelected)
        {
            return _magazineNavigationItem;
        }

        return ViewModel.SelectedCategory ?? _navigationItems.FirstOrDefault() ?? _magazineNavigationItem;
    }

    private void RebuildNavigationItems()
    {
        _navigationItems.Clear();
        _navigationItems.Add(_magazineNavigationItem);

        foreach (var category in ViewModel.Categories)
        {
            _navigationItems.Add(category);
        }
    }

    private static bool TryGetLatinLetter(VirtualKey key, out char value)
    {
        if (key is >= VirtualKey.A and <= VirtualKey.Z)
        {
            value = (char)('A' + (key - VirtualKey.A));
            return true;
        }

        value = '\0';
        return false;
    }
}
