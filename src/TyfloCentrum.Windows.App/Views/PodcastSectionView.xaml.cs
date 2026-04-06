using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class PodcastSectionView : UserControl
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromMinutes(6);
    private readonly IAudioPlaybackRequestFactory _audioPlaybackRequestFactory;
    private readonly IClipboardService _clipboardService;
    private readonly IContentDownloadService _contentDownloadService;
    private readonly ContentEntryActionService _contentEntryActionService;
    private readonly ContentFavoriteService _contentFavoriteService;
    private readonly PodcastShowNotesDialogService _podcastShowNotesDialogService;
    private readonly IPodcastShowNotesService _podcastShowNotesService;
    private readonly IShareService _shareService;
    private DispatcherQueueTimer? _autoRefreshTimer;
    private bool _loadMoreRequestPending;
    private ContentCategoryItemViewModel? _pendingFocusedCategory;
    private bool _restoreFocusToCategoriesList;
    private bool _synchronizingCategorySelection;

    public event EventHandler? ExitToSectionListRequested;

    public PodcastSectionView(
        PodcastCatalogViewModel viewModel,
        IAudioPlaybackRequestFactory audioPlaybackRequestFactory,
        IClipboardService clipboardService,
        IContentDownloadService contentDownloadService,
        ContentEntryActionService contentEntryActionService,
        ContentFavoriteService contentFavoriteService,
        IPodcastShowNotesService podcastShowNotesService,
        PodcastShowNotesDialogService podcastShowNotesDialogService,
        IShareService shareService
    )
    {
        ViewModel = viewModel;
        _audioPlaybackRequestFactory = audioPlaybackRequestFactory;
        _clipboardService = clipboardService;
        _contentDownloadService = contentDownloadService;
        _contentEntryActionService = contentEntryActionService;
        _contentFavoriteService = contentFavoriteService;
        _podcastShowNotesService = podcastShowNotesService;
        _podcastShowNotesDialogService = podcastShowNotesDialogService;
        _shareService = shareService;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Categories.CollectionChanged += OnCategoriesCollectionChanged;
        ViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
        UpdateVisualState();
    }

    public PodcastCatalogViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        if (ViewModel.HasCategories && ViewModel.SelectedCategory is not null)
        {
            ListViewFocusHelper.RestoreFocus(CategoriesList, ViewModel.SelectedCategory);
            return;
        }

        if (ViewModel.Categories.Count > 0)
        {
            ListViewFocusHelper.RestoreFocus(CategoriesList, ViewModel.Categories[0]);
            return;
        }

        if (ViewModel.HasItems && ViewModel.Items.Count > 0)
        {
            ListViewFocusHelper.RestoreFocus(ItemsList, ViewModel.Items[0]);
            return;
        }

        CategoriesList.Focus(FocusState.Programmatic);
    }

    public async Task PrepareForScreenshotAsync()
    {
        await ViewModel.LoadIfNeededAsync();

        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (!ViewModel.IsLoading && (ViewModel.HasLoaded || ViewModel.HasError))
            {
                break;
            }

            await Task.Delay(100);
        }

        if (ViewModel.Categories.Count > 0)
        {
            var targetCategory = ViewModel.SelectedCategory ?? ViewModel.Categories[0];
            await SelectCategoryFromUiAsync(targetCategory);
        }

        if (ViewModel.HasItems && ViewModel.Items.Count > 0)
        {
            ItemsList.SelectedItem = ViewModel.Items[0];
        }

        UpdateLayout();
        await Task.Delay(200);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureAutoRefreshTimer();
        await ViewModel.LoadIfNeededAsync();
        await ViewModel.RefreshIfStaleAsync(AutoRefreshInterval);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_autoRefreshTimer is null)
        {
            return;
        }

        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Tick -= OnAutoRefreshTimerTick;
        _autoRefreshTimer = null;
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

        if (sender is ListView { SelectedItem: ContentCategoryItemViewModel category })
        {
            _pendingFocusedCategory = category;
            _restoreFocusToCategoriesList = FocusNavigationHelper.IsFocusWithin(CategoriesList);
            await SelectCategoryFromUiAsync(category);
            RestorePendingCategoryFocus();
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
            if (categoryItems.Count == 0)
            {
                return;
            }

            var currentCategoryIndex = Math.Max(
                0,
                categoryItems.IndexOf(ViewModel.SelectedCategory ?? categoryItems[0])
            );
            var matchedCategory = InitialListNavigationHelper.FindNextByInitial(
                categoryItems,
                currentCategoryIndex,
                category => category.Name,
                input
            );

            if (matchedCategory is not null)
            {
                e.Handled = true;
                _pendingFocusedCategory = matchedCategory;
                _restoreFocusToCategoriesList = FocusNavigationHelper.IsFocusWithin(CategoriesList);
                _synchronizingCategorySelection = true;
                listView.SelectedItem = matchedCategory;
                _synchronizingCategorySelection = false;
                await SelectCategoryFromUiAsync(matchedCategory);
                RestorePendingCategoryFocus();
            }

            return;
        }

        if (e.Key is not (VirtualKey.Up or VirtualKey.Down))
        {
            return;
        }

        var categories = ViewModel.Categories;
        if (categories.Count == 0)
        {
            return;
        }

        var currentIndex = Math.Max(0, categories.IndexOf(ViewModel.SelectedCategory ?? categories[0]));
        var nextIndex = e.Key == VirtualKey.Down ? currentIndex + 1 : currentIndex - 1;
        if (nextIndex < 0 || nextIndex >= categories.Count)
        {
            return;
        }

        var nextCategory = categories[nextIndex];
        e.Handled = true;
        _pendingFocusedCategory = nextCategory;
        _restoreFocusToCategoriesList = FocusNavigationHelper.IsFocusWithin(CategoriesList);
        _synchronizingCategorySelection = true;
        listView.SelectedItem = nextCategory;
        _synchronizingCategorySelection = false;
        await SelectCategoryFromUiAsync(nextCategory);
        RestorePendingCategoryFocus();
    }

    private async void OnItemsListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ContentPostItemViewModel item)
        {
            await OpenDefaultActionAsync(item);
        }
    }

    private void EnsureAutoRefreshTimer()
    {
        if (_autoRefreshTimer is not null)
        {
            return;
        }

        _autoRefreshTimer = DispatcherQueue.CreateTimer();
        _autoRefreshTimer.Interval = AutoRefreshInterval;
        _autoRefreshTimer.IsRepeating = true;
        _autoRefreshTimer.Tick += OnAutoRefreshTimerTick;
        _autoRefreshTimer.Start();
    }

    private async void OnAutoRefreshTimerTick(DispatcherQueueTimer sender, object args)
    {
        await ViewModel.RefreshIfStaleAsync(AutoRefreshInterval);
    }

    private async void OnItemsListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (
            !KeyboardShortcutHelper.IsControlPressed()
            && !KeyboardShortcutHelper.IsAltPressed()
            && TryGetLatinLetter(e.Key, out var input)
            && sender is ListView listView
        )
        {
            var items = ViewModel.Items;
            if (items.Count == 0)
            {
                return;
            }

            var currentIndex = listView.SelectedItem is ContentPostItemViewModel selectedListItem
                ? items.IndexOf(selectedListItem)
                : -1;
            var matchedItem = InitialListNavigationHelper.FindNextByInitial(
                items,
                currentIndex,
                item => item.Title,
                input
            );

            if (matchedItem is not null)
            {
                e.Handled = true;
                ListViewFocusHelper.RestoreFocus(listView, matchedItem);
            }

            return;
        }

        if (
            KeyboardShortcutHelper.IsControlPressed()
            && sender is ListView { SelectedItem: ContentPostItemViewModel selectedItem }
        )
        {
            switch (e.Key)
            {
                case VirtualKey.D:
                    e.Handled = true;
                    await ToggleFavoriteAsync(selectedItem);
                    return;
                case VirtualKey.S:
                    e.Handled = true;
                    await DownloadItemAsync(selectedItem);
                    return;
                case VirtualKey.C:
                    e.Handled = true;
                    await CopyPodcastPageLinkAsync(selectedItem);
                    return;
                case VirtualKey.P:
                    e.Handled = true;
                    await CopyPodcastAudioLinkAsync(selectedItem);
                    return;
                case VirtualKey.U:
                    e.Handled = true;
                    await ShareItemAsync(selectedItem);
                    return;
            }
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

    private void OnItemsListContainerContentChanging(
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
            QueueLoadMore();
        }
    }

    private void QueueLoadMore()
    {
        if (_loadMoreRequestPending)
        {
            return;
        }

        _loadMoreRequestPending = true;
        if (
            !DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await ViewModel.LoadMoreAsync();
                }
                finally
                {
                    _loadMoreRequestPending = false;
                }
            })
        )
        {
            _loadMoreRequestPending = false;
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
        var playItem = new MenuFlyoutItem { Text = "Odtwórz" };
        AutomationProperties.SetName(playItem, item.DefaultActionLabel);
        playItem.Click += async (_, _) => await OpenDefaultActionAsync(item);
        flyout.Items.Add(playItem);

        var browserItem = new MenuFlyoutItem { Text = "Otwórz w przeglądarce" };
        AutomationProperties.SetName(browserItem, item.OpenLinkLabel);
        browserItem.Click += async (_, _) => await ViewModel.OpenItemAsync(item);
        flyout.Items.Add(browserItem);

        var copyPageLinkItem = new MenuFlyoutItem { Text = "Kopiuj adres strony podcastu (Ctrl+C)" };
        AutomationProperties.SetName(
            copyPageLinkItem,
            $"Kopiuj adres strony podcastu (Ctrl+C): {item.Title}"
        );
        copyPageLinkItem.Click += async (_, _) => await CopyPodcastPageLinkAsync(item);
        flyout.Items.Add(copyPageLinkItem);

        var copyAudioLinkItem = new MenuFlyoutItem { Text = "Kopiuj adres podcastu (Ctrl+P)" };
        AutomationProperties.SetName(
            copyAudioLinkItem,
            $"Kopiuj adres podcastu (Ctrl+P): {item.Title}"
        );
        copyAudioLinkItem.Click += async (_, _) => await CopyPodcastAudioLinkAsync(item);
        flyout.Items.Add(copyAudioLinkItem);

        if (await TryGetPodcastShowNotesAsync(item.PostId) is { } showNotes)
        {
            AddPodcastShowNotesMenuItems(flyout, item, showNotes);
        }

        var downloadItem = new MenuFlyoutItem { Text = "Pobierz (Ctrl+S)" };
        AutomationProperties.SetName(downloadItem, $"Pobierz podcast (Ctrl+S): {item.Title}");
        downloadItem.Click += async (_, _) => await DownloadItemAsync(item);
        flyout.Items.Add(downloadItem);

        var shareItem = new MenuFlyoutItem { Text = "Udostępnij (Ctrl+U)" };
        AutomationProperties.SetName(shareItem, $"Udostępnij podcast (Ctrl+U): {item.Title}");
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

    private void AddPodcastShowNotesMenuItems(
        MenuFlyout flyout,
        ContentPostItemViewModel item,
        PodcastShowNotesSnapshot showNotes
    )
    {
        if (showNotes.HasComments)
        {
            var commentsItem = new MenuFlyoutItem { Text = "Pokaż komentarze" };
            AutomationProperties.SetName(commentsItem, $"Pokaż komentarze podcastu: {item.Title}");
            commentsItem.Click += async (_, _) =>
                await ShowPodcastShowNotesSectionAsync(PodcastShowNotesSection.Comments, item, showNotes);
            flyout.Items.Add(commentsItem);
        }

        if (showNotes.HasChapterMarkers)
        {
            var chapterMarkersItem = new MenuFlyoutItem { Text = "Pokaż znaczniki czasu" };
            AutomationProperties.SetName(
                chapterMarkersItem,
                $"Pokaż znaczniki czasu podcastu: {item.Title}"
            );
            chapterMarkersItem.Click += async (_, _) =>
                await ShowPodcastShowNotesSectionAsync(
                    PodcastShowNotesSection.ChapterMarkers,
                    item,
                    showNotes
                );
            flyout.Items.Add(chapterMarkersItem);
        }

        if (showNotes.HasRelatedLinks)
        {
            var relatedLinksItem = new MenuFlyoutItem { Text = "Pokaż odnośniki" };
            AutomationProperties.SetName(
                relatedLinksItem,
                $"Pokaż odnośniki podcastu: {item.Title}"
            );
            relatedLinksItem.Click += async (_, _) =>
                await ShowPodcastShowNotesSectionAsync(
                    PodcastShowNotesSection.RelatedLinks,
                    item,
                    showNotes
                );
            flyout.Items.Add(relatedLinksItem);
        }
    }

    private async Task<PodcastShowNotesSnapshot?> TryGetPodcastShowNotesAsync(int postId)
    {
        try
        {
            return await _podcastShowNotesService.GetAsync(postId);
        }
        catch
        {
            return null;
        }
    }

    private async Task ShowPodcastShowNotesSectionAsync(
        PodcastShowNotesSection section,
        ContentPostItemViewModel item,
        PodcastShowNotesSnapshot showNotes
    )
    {
        var shown = await _podcastShowNotesDialogService.ShowAsync(
            section,
            item.PostId,
            item.Title,
            item.PublishedDate,
            showNotes,
            XamlRoot
        );

        if (!shown)
        {
            await DialogHelpers.ShowErrorAsync(
                XamlRoot,
                section switch
                {
                    PodcastShowNotesSection.Comments =>
                        "Nie udało się wyświetlić komentarzy podcastu.",
                    PodcastShowNotesSection.ChapterMarkers =>
                        "Nie udało się wyświetlić znaczników czasu podcastu.",
                    PodcastShowNotesSection.RelatedLinks =>
                        "Nie udało się wyświetlić odnośników podcastu.",
                    _ => "Nie udało się wyświetlić dodatków podcastu.",
                }
            );
        }

        ListViewFocusHelper.RestoreFocus(ItemsList, item);
    }

    private async Task CopyPodcastPageLinkAsync(ContentPostItemViewModel item)
    {
        var copied = await _clipboardService.SetTextAsync(item.Link);
        if (!copied)
        {
            await DialogHelpers.ShowErrorAsync(
                XamlRoot,
                "Nie udało się skopiować adresu strony podcastu."
            );
            ListViewFocusHelper.RestoreFocus(ItemsList, item);
            return;
        }

        AutomationAnnouncementHelper.Announce(
            ItemsList,
            $"Skopiowano adres strony podcastu: {item.Title}.",
            important: true
        );
        ListViewFocusHelper.RestoreFocus(ItemsList, item);
    }

    private async Task CopyPodcastAudioLinkAsync(ContentPostItemViewModel item)
    {
        var copied = await _clipboardService.SetTextAsync(
            _audioPlaybackRequestFactory.CreatePodcastDownloadUri(item.PostId).ToString()
        );
        if (!copied)
        {
            await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się skopiować adresu podcastu.");
            ListViewFocusHelper.RestoreFocus(ItemsList, item);
            return;
        }

        AutomationAnnouncementHelper.Announce(
            ItemsList,
            $"Skopiowano adres podcastu: {item.Title}.",
            important: true
        );
        ListViewFocusHelper.RestoreFocus(ItemsList, item);
    }

    private async Task OpenDefaultActionAsync(ContentPostItemViewModel item)
    {
        AutomationAnnouncementHelper.Announce(
            ItemsList,
            $"Otwieranie odtwarzacza podcastu: {item.Title}.",
            important: true
        );
        var shown = await _contentEntryActionService.OpenDefaultAsync(item, XamlRoot);
        if (!shown)
        {
            await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się uruchomić odtwarzacza podcastu.");
        }

        ListViewFocusHelper.RestoreFocus(ItemsList, item);
    }

    private async Task ShareItemAsync(ContentPostItemViewModel item)
    {
        var shared = await _shareService.ShareLinkAsync(item.Title, item.Excerpt, item.Link);
        if (!shared)
        {
            await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się udostępnić podcastu.");
        }

        ListViewFocusHelper.RestoreFocus(ItemsList, item);
    }

    private async Task DownloadItemAsync(ContentPostItemViewModel item)
    {
        try
        {
            var filePath = await _contentDownloadService.DownloadPodcastAsync(item.PostId, item.Title);
            AutomationAnnouncementHelper.Announce(
                ItemsList,
                $"Pobrano podcast: {Path.GetFileName(filePath)}.",
                important: true
            );
        }
        catch
        {
            await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się pobrać podcastu.");
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
        UpdateVisualState();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            ListViewFocusHelper.RestoreFocusedSelectionIfNeeded(ItemsList);
        }

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
        ItemsList.Visibility = ViewModel.HasItems ? Visibility.Visible : Visibility.Collapsed;
        CategoriesList.Visibility = ViewModel.HasCategories ? Visibility.Visible : Visibility.Collapsed;
        LoadMoreIndicator.IsActive = ViewModel.IsLoadingMore;
        LoadMoreIndicator.Visibility = ViewModel.IsLoadingMore ? Visibility.Visible : Visibility.Collapsed;

        if (!ReferenceEquals(CategoriesList.SelectedItem, ViewModel.SelectedCategory))
        {
            _synchronizingCategorySelection = true;
            CategoriesList.SelectedItem = ViewModel.SelectedCategory;
            _synchronizingCategorySelection = false;
        }

        RestorePendingCategoryFocus();
    }

    private async Task SelectCategoryFromUiAsync(ContentCategoryItemViewModel category)
    {
        if (ReferenceEquals(category, ViewModel.SelectedCategory))
        {
            RestorePendingCategoryFocus();
            return;
        }

        await ViewModel.SelectCategoryAsync(category);
    }

    private void RestorePendingCategoryFocus()
    {
        if (ViewModel.IsLoading || _pendingFocusedCategory is null)
        {
            return;
        }

        if (!ReferenceEquals(_pendingFocusedCategory, ViewModel.SelectedCategory))
        {
            _pendingFocusedCategory = null;
            _restoreFocusToCategoriesList = false;
            return;
        }

        CategoriesList.SelectedItem = _pendingFocusedCategory;
        CategoriesList.ScrollIntoView(_pendingFocusedCategory);
        CategoriesList.UpdateLayout();

        _pendingFocusedCategory = null;
        _restoreFocusToCategoriesList = false;
    }

    private void FocusItemsList()
    {
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
        if (e.Key == VirtualKey.Enter && FocusNavigationHelper.IsFocusWithin(CategoriesList))
        {
            e.Handled = true;
            FocusItemsList();
            return;
        }

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

        if (FocusNavigationHelper.IsFocusWithin(CategoriesList) || FocusNavigationHelper.IsFocusWithin(this))
        {
            e.Handled = true;
            ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
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
