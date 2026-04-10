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

public sealed partial class NewsSectionView : UserControl
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

    public event EventHandler? ExitToSectionListRequested;

    public NewsSectionView(
        NewsFeedViewModel viewModel,
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

        if (ViewModel.HasItems && ViewModel.Items.Count > 0)
        {
            NewsList.SelectedItem = ViewModel.Items[0];
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

            var currentIndex = listView.SelectedItem is NewsFeedItemViewModel selectedNewsItem
                ? items.IndexOf(selectedNewsItem)
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
            && sender is ListView { SelectedItem: NewsFeedItemViewModel selectedItem }
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
                    await CopyPageLinkAsync(selectedItem);
                    return;
                case VirtualKey.P when selectedItem.SupportsPlayback:
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

        if (sender is ListView { SelectedItem: NewsFeedItemViewModel item })
        {
            e.Handled = true;
            await OpenDefaultActionAsync(item);
        }
    }

    private void OnNewsListContainerContentChanging(
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

        var browserItem = new MenuFlyoutItem { Text = "Otwórz w przeglądarce" };
        AutomationProperties.SetName(browserItem, item.OpenLinkLabel);
        browserItem.Click += async (_, _) => await ViewModel.OpenItemAsync(item);
        flyout.Items.Add(browserItem);

        var copyPageLinkItem = new MenuFlyoutItem
        {
            Text = item.SupportsPlayback
                ? "Kopiuj adres strony podcastu (Ctrl+C)"
                : "Kopiuj adres artykułu (Ctrl+C)",
        };
        AutomationProperties.SetName(
            copyPageLinkItem,
            item.SupportsPlayback
                ? $"Kopiuj adres strony podcastu (Ctrl+C): {item.Title}"
                : $"Kopiuj adres artykułu (Ctrl+C): {item.Title}"
        );
        copyPageLinkItem.Click += async (_, _) => await CopyPageLinkAsync(item);
        flyout.Items.Add(copyPageLinkItem);

        if (item.SupportsPlayback)
        {
            var copyAudioLinkItem = new MenuFlyoutItem { Text = "Kopiuj adres podcastu (Ctrl+P)" };
            AutomationProperties.SetName(
                copyAudioLinkItem,
                $"Kopiuj adres podcastu (Ctrl+P): {item.Title}"
            );
            copyAudioLinkItem.Click += async (_, _) => await CopyPodcastAudioLinkAsync(item);
            flyout.Items.Add(copyAudioLinkItem);

            AddPodcastShowNotesMenuItems(flyout, item, await TryGetPodcastShowNotesAsync(item.PostId));
        }

        var downloadItem = new MenuFlyoutItem { Text = "Pobierz (Ctrl+S)" };
        AutomationProperties.SetName(
            downloadItem,
            item.SupportsPlayback
                ? $"Pobierz podcast (Ctrl+S): {item.Title}"
                : $"Pobierz artykuł (Ctrl+S): {item.Title}"
        );
        downloadItem.Click += async (_, _) => await DownloadItemAsync(item);
        flyout.Items.Add(downloadItem);

        var shareItem = new MenuFlyoutItem { Text = "Udostępnij (Ctrl+U)" };
        AutomationProperties.SetName(
            shareItem,
            item.SupportsPlayback
                ? $"Udostępnij podcast (Ctrl+U): {item.Title}"
                : $"Udostępnij artykuł (Ctrl+U): {item.Title}"
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

    private void AddPodcastShowNotesMenuItems(
        MenuFlyout flyout,
        NewsFeedItemViewModel item,
        PodcastShowNotesSnapshot showNotes
    )
    {
        var commentsItem = new MenuFlyoutItem
        {
            Text = showNotes.HasComments ? "Pokaż komentarze" : "Dodaj komentarz",
        };
        AutomationProperties.SetName(
            commentsItem,
            showNotes.HasComments
                ? $"Pokaż komentarze podcastu: {item.Title}"
                : $"Dodaj komentarz do podcastu: {item.Title}"
        );
        commentsItem.Click += async (_, _) =>
            await ShowPodcastShowNotesSectionAsync(PodcastShowNotesSection.Comments, item, showNotes);
        flyout.Items.Add(commentsItem);

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

    private async Task<PodcastShowNotesSnapshot> TryGetPodcastShowNotesAsync(int postId)
    {
        try
        {
            return await _podcastShowNotesService.GetAsync(postId);
        }
        catch
        {
            return new PodcastShowNotesSnapshot([], [], []);
        }
    }

    private async Task ShowPodcastShowNotesSectionAsync(
        PodcastShowNotesSection section,
        NewsFeedItemViewModel item,
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

        ListViewFocusHelper.RestoreFocus(NewsList, item);
    }

    private async Task CopyPageLinkAsync(NewsFeedItemViewModel item)
    {
        var copied = await _clipboardService.SetTextAsync(item.Link);
        if (!copied)
        {
            var message = item.SupportsPlayback
                ? "Nie udało się skopiować adresu strony podcastu."
                : "Nie udało się skopiować adresu artykułu.";
            await DialogHelpers.ShowErrorAsync(XamlRoot, message);
            ListViewFocusHelper.RestoreFocus(NewsList, item);
            return;
        }

        AutomationAnnouncementHelper.Announce(
            NewsList,
            item.SupportsPlayback
                ? $"Skopiowano adres strony podcastu: {item.Title}."
                : $"Skopiowano adres artykułu: {item.Title}.",
            important: true
        );
        ListViewFocusHelper.RestoreFocus(NewsList, item);
    }

    private async Task CopyPodcastAudioLinkAsync(NewsFeedItemViewModel item)
    {
        var copied = await _clipboardService.SetTextAsync(
            _audioPlaybackRequestFactory.CreatePodcastDownloadUri(item.PostId).ToString()
        );
        if (!copied)
        {
            await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się skopiować adresu podcastu.");
            ListViewFocusHelper.RestoreFocus(NewsList, item);
            return;
        }

        AutomationAnnouncementHelper.Announce(
            NewsList,
            $"Skopiowano adres podcastu: {item.Title}.",
            important: true
        );
        ListViewFocusHelper.RestoreFocus(NewsList, item);
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

    private async Task DownloadItemAsync(NewsFeedItemViewModel item)
    {
        try
        {
            var filePath = item.SupportsPlayback
                ? await _contentDownloadService.DownloadPodcastAsync(item.PostId, item.Title)
                : await _contentDownloadService.DownloadArticleAsync(
                    item.Source,
                    item.PostId,
                    item.Title,
                    item.PublishedDate,
                    item.Link
                );

            AutomationAnnouncementHelper.Announce(
                NewsList,
                item.SupportsPlayback
                    ? $"Pobrano podcast: {Path.GetFileName(filePath)}."
                    : $"Pobrano artykuł: {Path.GetFileName(filePath)}.",
                important: true
            );
        }
        catch
        {
            var message = item.SupportsPlayback
                ? "Nie udało się pobrać podcastu."
                : "Nie udało się pobrać artykułu.";
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
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            ListViewFocusHelper.RestoreFocusedSelectionIfNeeded(NewsList);
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
