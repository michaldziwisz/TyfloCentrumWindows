using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class NewsSectionView : UserControl
{
    private readonly ContentEntryActionService _contentEntryActionService;
    private readonly ContentFavoriteService _contentFavoriteService;
    private readonly PostDetailDialogService _postDetailDialogService;
    private readonly IShareService _shareService;

    public event EventHandler? ExitToSectionListRequested;

    public NewsSectionView(
        NewsFeedViewModel viewModel,
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
        ViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
        UpdateVisualState();
    }

    public NewsFeedViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        if (ViewModel.HasItems && ViewModel.Items.Count > 0)
        {
            var selectedItem = NewsList.SelectedItem as NewsFeedItemViewModel ?? ViewModel.Items[0];
            ListViewFocusHelper.RestoreFocus(NewsList, selectedItem);
            return;
        }

        if (ViewModel.HasError)
        {
            ErrorPanel.Focus(FocusState.Programmatic);
            return;
        }

        NewsList.Focus(FocusState.Programmatic);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadIfNeededAsync();
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryAsync();
    }

    private async void OnNewsListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NewsFeedItemViewModel item)
        {
            await OpenDefaultActionAsync(item);
        }
    }

    private async void OnNewsListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (
            e.Key == VirtualKey.D
            && KeyboardShortcutHelper.IsControlPressed()
            && sender is ListView { SelectedItem: NewsFeedItemViewModel favoriteItem }
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

        if (sender is ListView { SelectedItem: NewsFeedItemViewModel item })
        {
            e.Handled = true;
            await OpenDefaultActionAsync(item);
        }
    }

    private async void OnNewsListContainerContentChanging(
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

    private async void OnNewsListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var item =
            ItemContextResolver.Resolve<NewsFeedItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as NewsFeedItemViewModel;

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
        browserItem.Click += async (_, _) => await ViewModel.OpenItemAsync(item);
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

    private async Task OpenDefaultActionAsync(NewsFeedItemViewModel item)
    {
        AutomationAnnouncementHelper.Announce(
            NewsList,
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

        ListViewFocusHelper.RestoreFocus(NewsList, item);
    }

    private Task OpenDetailsAsync(NewsFeedItemViewModel item)
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

    private async Task ToggleFavoriteAsync(NewsFeedItemViewModel item)
    {
        try
        {
            var isFavorite = await _contentFavoriteService.ToggleAsync(item);
            AutomationAnnouncementHelper.Announce(
                NewsList,
                isFavorite
                    ? $"Dodano do ulubionych: {item.Title}."
                    : $"Usunięto z ulubionych: {item.Title}.",
                important: true
            );
            ListViewFocusHelper.RestoreFocus(NewsList, item);
        }
        catch
        {
            await DialogHelpers.ShowErrorAsync(
                XamlRoot,
                "Nie udało się zaktualizować ulubionego wpisu."
            );
        }
    }

    private async Task ShareItemAsync(NewsFeedItemViewModel item)
    {
        var shared = await _shareService.ShareLinkAsync(item.Title, item.Excerpt, item.Link);
        if (!shared)
        {
            var message = item.SupportsPlayback
                ? "Nie udało się udostępnić podcastu."
                : "Nie udało się udostępnić artykułu.";
            await DialogHelpers.ShowErrorAsync(XamlRoot, message);
        }

        ListViewFocusHelper.RestoreFocus(NewsList, item);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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

        EmptyStateText.Visibility = ViewModel.ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        NewsList.Visibility = ViewModel.HasItems ? Visibility.Visible : Visibility.Collapsed;
        LoadMoreIndicator.IsActive = ViewModel.IsLoadingMore;
        LoadMoreIndicator.Visibility = ViewModel.IsLoadingMore ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        if (FocusNavigationHelper.IsFocusWithin(NewsList) || FocusNavigationHelper.IsFocusWithin(this))
        {
            e.Handled = true;
            ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
