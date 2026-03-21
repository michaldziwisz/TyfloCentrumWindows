using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using Tyflocentrum.Windows.App.Services;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.UI.Services;
using Tyflocentrum.Windows.UI.ViewModels;
using Windows.System;

namespace Tyflocentrum.Windows.App.Views;

public sealed partial class SearchSectionView : UserControl
{
    private readonly ContentEntryActionService _contentEntryActionService;
    private readonly ContentFavoriteService _contentFavoriteService;
    private readonly PostDetailDialogService _postDetailDialogService;
    private readonly IShareService _shareService;
    private bool _focusResultsAfterSearch;

    public event EventHandler? ExitToSectionListRequested;

    public SearchSectionView(
        SearchViewModel viewModel,
        ContentEntryActionService contentEntryActionService,
        ContentFavoriteService contentFavoriteService,
        IShareService shareService,
        PostDetailDialogService postDetailDialogService
    )
    {
        ViewModel = viewModel;
        _contentEntryActionService = contentEntryActionService;
        _contentFavoriteService = contentFavoriteService;
        _shareService = shareService;
        _postDetailDialogService = postDetailDialogService;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Results.CollectionChanged += OnResultsCollectionChanged;
        UpdateVisualState();
    }

    public SearchViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        DispatcherQueue.TryEnqueue(() => SearchTextBox.Focus(FocusState.Programmatic));
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        _focusResultsAfterSearch = true;
        await ViewModel.SearchAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryAsync();
    }

    private async void OnSearchTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.CanSearch)
        {
            e.Handled = true;
            _focusResultsAfterSearch = true;
            await ViewModel.SearchAsync();
        }
    }

    private async void OnResultsListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ContentPostItemViewModel item)
        {
            await OpenDefaultActionAsync(item);
        }
    }

    private async void OnResultsListKeyDown(object sender, KeyRoutedEventArgs e)
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

    private async void OnResultsListContextRequested(object sender, ContextRequestedEventArgs e)
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
        var defaultText = item.SupportsPlayback ? "Odtwórz" : "Otwórz artykuł";
        var openItem = new MenuFlyoutItem { Text = defaultText };
        AutomationProperties.SetName(openItem, item.DefaultActionLabel);
        openItem.Click += async (_, _) => await OpenDefaultActionAsync(item);
        flyout.Items.Add(openItem);

        var detailsItem = new MenuFlyoutItem { Text = "Szczegóły" };
        AutomationProperties.SetName(detailsItem, item.OpenDetailsLabel);
        detailsItem.Click += async (_, _) => await OpenDetailsAsync(item);
        flyout.Items.Add(detailsItem);

        var browserItem = new MenuFlyoutItem { Text = "Otwórz w przeglądarce" };
        AutomationProperties.SetName(browserItem, item.OpenLinkLabel);
        browserItem.Click += async (_, _) => await ViewModel.OpenResultAsync(item);
        flyout.Items.Add(browserItem);

        var shareItem = new MenuFlyoutItem { Text = "Udostępnij" };
        AutomationProperties.SetName(
            shareItem,
            item.SupportsPlayback
                ? $"Udostępnij podcast: {item.Title}"
                : $"Udostępnij artykuł: {item.Title}"
        );
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
            ResultsList,
            item.SupportsPlayback
                ? $"Otwieranie odtwarzacza podcastu: {item.Title}."
                : $"Otwieranie artykułu: {item.Title}.",
            important: true
        );
        var shown = await _contentEntryActionService.OpenDefaultAsync(item, XamlRoot);
        if (!shown)
        {
            var message = item.SupportsPlayback
                ? "Nie udało się uruchomić odtwarzacza podcastu."
                : "Nie udało się otworzyć artykułu w aplikacji.";
            await DialogHelpers.ShowErrorAsync(XamlRoot, message);
        }

        ListViewFocusHelper.RestoreFocus(ResultsList, item);
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

    private async Task ToggleFavoriteAsync(ContentPostItemViewModel item)
    {
        try
        {
            var isFavorite = await _contentFavoriteService.ToggleAsync(item);
            ViewModel.StatusMessage = isFavorite
                ? $"Dodano do ulubionych: {item.Title}."
                : $"Usunięto z ulubionych: {item.Title}.";
            AutomationAnnouncementHelper.Announce(StatusTextBlock, ViewModel.StatusMessage, important: true);
            ListViewFocusHelper.RestoreFocus(ResultsList, item);
        }
        catch
        {
            await DialogHelpers.ShowErrorAsync(
                XamlRoot,
                "Nie udało się zaktualizować ulubionego wpisu."
            );
        }
    }

    private async Task ShareItemAsync(ContentPostItemViewModel item)
    {
        var shared = await _shareService.ShareLinkAsync(item.Title, item.Excerpt, item.Link);
        if (!shared)
        {
            var message = item.SupportsPlayback
                ? "Nie udało się udostępnić podcastu."
                : "Nie udało się udostępnić artykułu.";
            await DialogHelpers.ShowErrorAsync(XamlRoot, message);
        }

        ListViewFocusHelper.RestoreFocus(ResultsList, item);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void OnResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        EmptyStateText.Visibility = ViewModel.ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = ViewModel.HasResults ? Visibility.Visible : Visibility.Collapsed;
        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.StatusMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ScopeComboBox.IsEnabled = true;
        SearchTextBox.IsEnabled = true;
        SearchTextBox.IsReadOnly = ViewModel.IsLoading;
        ResultsList.IsEnabled = true;

        if (_focusResultsAfterSearch && !ViewModel.IsLoading)
        {
            _focusResultsAfterSearch = false;

            if (ViewModel.HasResults && ViewModel.Results.Count > 0)
            {
                ListViewFocusHelper.RestoreFocus(ResultsList, ViewModel.Results[0]);
            }
            else
            {
                DispatcherQueue.TryEnqueue(() => SearchTextBox.Focus(FocusState.Programmatic));
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        if (FocusNavigationHelper.IsFocusWithin(ResultsList))
        {
            e.Handled = true;
            SearchTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (FocusNavigationHelper.IsFocusWithin(this))
        {
            e.Handled = true;
            ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
