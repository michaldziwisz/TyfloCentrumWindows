using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class PodcastShowNotesDialogView : UserControl
{
    private PodcastShowNotesSection _section;
    private CommentItemViewModel[] _comments = [];
    private PodcastChapterMarkerItemViewModel[] _chapterMarkers = [];
    private PodcastRelatedLinkItemViewModel[] _relatedLinks = [];
    private Func<PodcastChapterMarkerItemViewModel, Task<bool>>? _activateChapterMarkerAsync;
    private Func<PodcastChapterMarkerItemViewModel, Task<bool>>? _toggleChapterMarkerFavoriteAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _copyRelatedLinkAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _openRelatedLinkAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _shareRelatedLinkAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _toggleRelatedLinkFavoriteAsync;

    public PodcastShowNotesDialogView()
    {
        InitializeComponent();
    }

    public void InitializeComments(
        int podcastPostId,
        string podcastTitle,
        string podcastSubtitle,
        CommentItemViewModel[] comments
    )
    {
        _section = PodcastShowNotesSection.Comments;
        _comments = comments;
        _chapterMarkers = [];
        _relatedLinks = [];
        _activateChapterMarkerAsync = null;
        _toggleChapterMarkerFavoriteAsync = null;
        _copyRelatedLinkAsync = null;
        _openRelatedLinkAsync = null;
        _shareRelatedLinkAsync = null;
        _toggleRelatedLinkFavoriteAsync = null;
        PodcastTitleTextBlock.Text = podcastTitle;
        SectionHintTextBlock.Text =
            "Enter rozwija albo zwija pełną treść zaznaczonego komentarza.";
        AutomationProperties.SetName(ItemsList, "Komentarze podcastu");
        ItemsList.ItemTemplate = (DataTemplate)Resources["CommentItemTemplate"];
        ItemsList.ItemsSource = _comments;
        ItemsList.SelectedItem = _comments.FirstOrDefault();
        SetStatusMessage(null);
    }

    public void InitializeChapterMarkers(
        int podcastPostId,
        string podcastTitle,
        string podcastSubtitle,
        PodcastChapterMarkerItemViewModel[] chapterMarkers,
        Func<PodcastChapterMarkerItemViewModel, Task<bool>> activateChapterMarkerAsync,
        Func<PodcastChapterMarkerItemViewModel, Task<bool>> toggleChapterMarkerFavoriteAsync
    )
    {
        _section = PodcastShowNotesSection.ChapterMarkers;
        _comments = [];
        _chapterMarkers = chapterMarkers;
        _relatedLinks = [];
        _activateChapterMarkerAsync = activateChapterMarkerAsync;
        _toggleChapterMarkerFavoriteAsync = toggleChapterMarkerFavoriteAsync;
        _copyRelatedLinkAsync = null;
        _openRelatedLinkAsync = null;
        _shareRelatedLinkAsync = null;
        _toggleRelatedLinkFavoriteAsync = null;
        PodcastTitleTextBlock.Text = podcastTitle;
        SectionHintTextBlock.Text =
            "Enter uruchamia odtwarzacz podcastu od zaznaczonego znacznika czasu. Ctrl+D dodaje albo usuwa znacznik z ulubionych.";
        AutomationProperties.SetName(ItemsList, "Znaczniki czasu podcastu");
        ItemsList.ItemTemplate = (DataTemplate)Resources["ChapterMarkerItemTemplate"];
        ItemsList.ItemsSource = _chapterMarkers;
        ItemsList.SelectedItem = _chapterMarkers.FirstOrDefault();
        SetStatusMessage(null);
    }

    public void InitializeRelatedLinks(
        int podcastPostId,
        string podcastTitle,
        string podcastSubtitle,
        PodcastRelatedLinkItemViewModel[] relatedLinks,
        Func<PodcastRelatedLinkItemViewModel, Task<bool>> openRelatedLinkAsync,
        Func<PodcastRelatedLinkItemViewModel, Task<bool>> copyRelatedLinkAsync,
        Func<PodcastRelatedLinkItemViewModel, Task<bool>> shareRelatedLinkAsync,
        Func<PodcastRelatedLinkItemViewModel, Task<bool>> toggleRelatedLinkFavoriteAsync
    )
    {
        _section = PodcastShowNotesSection.RelatedLinks;
        _comments = [];
        _chapterMarkers = [];
        _relatedLinks = relatedLinks;
        _activateChapterMarkerAsync = null;
        _toggleChapterMarkerFavoriteAsync = null;
        _copyRelatedLinkAsync = copyRelatedLinkAsync;
        _openRelatedLinkAsync = openRelatedLinkAsync;
        _shareRelatedLinkAsync = shareRelatedLinkAsync;
        _toggleRelatedLinkFavoriteAsync = toggleRelatedLinkFavoriteAsync;
        PodcastTitleTextBlock.Text = podcastTitle;
        SectionHintTextBlock.Text =
            "Enter otwiera zaznaczony odnośnik w przeglądarce. Ctrl+U udostępnia, a Ctrl+D dodaje albo usuwa odnośnik z ulubionych.";
        AutomationProperties.SetName(ItemsList, "Odnośniki podcastu");
        ItemsList.ItemTemplate = (DataTemplate)Resources["RelatedLinkItemTemplate"];
        ItemsList.ItemsSource = _relatedLinks;
        ItemsList.SelectedItem = _relatedLinks.FirstOrDefault();
        SetStatusMessage(null);
    }

    private async void OnItemsListItemClick(object sender, ItemClickEventArgs e)
    {
        await ActivateItemAsync(e.ClickedItem);
    }

    private async void OnItemsListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (
            KeyboardShortcutHelper.IsControlPressed()
            && e.Key == VirtualKey.D
            && ItemsList.SelectedItem is not null
        )
        {
            e.Handled = true;

            if (
                _section == PodcastShowNotesSection.ChapterMarkers
                && ItemsList.SelectedItem is PodcastChapterMarkerItemViewModel chapterMarker
            )
            {
                await ToggleChapterMarkerFavoriteAsync(chapterMarker);
                return;
            }

            if (
                _section == PodcastShowNotesSection.RelatedLinks
                && ItemsList.SelectedItem is PodcastRelatedLinkItemViewModel relatedLink
            )
            {
                await ToggleRelatedLinkFavoriteAsync(relatedLink);
                return;
            }
        }

        if (
            KeyboardShortcutHelper.IsControlPressed()
            && e.Key == VirtualKey.U
            && _section == PodcastShowNotesSection.RelatedLinks
            && ItemsList.SelectedItem is PodcastRelatedLinkItemViewModel selectedLink
        )
        {
            e.Handled = true;
            await ShareRelatedLinkAsync(selectedLink);
            return;
        }

        if (e.Key != VirtualKey.Enter || ItemsList.SelectedItem is null)
        {
            return;
        }

        e.Handled = true;
        await ActivateItemAsync(ItemsList.SelectedItem);
    }

    private void OnItemsListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        switch (_section)
        {
            case PodcastShowNotesSection.ChapterMarkers:
                ShowChapterMarkerContextMenu(listView, e);
                break;
            case PodcastShowNotesSection.RelatedLinks:
                ShowRelatedLinkContextMenu(listView, e);
                break;
        }
    }

    private void OnCommentDetailsClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommentItemViewModel item })
        {
            ToggleCommentDetails(item);
        }
    }

    private async Task ActivateItemAsync(object? item)
    {
        switch (_section)
        {
            case PodcastShowNotesSection.Comments when item is CommentItemViewModel comment:
                ToggleCommentDetails(comment);
                break;
            case PodcastShowNotesSection.ChapterMarkers when item is PodcastChapterMarkerItemViewModel chapterMarker:
                if (_activateChapterMarkerAsync is not null)
                {
                    var activated = await _activateChapterMarkerAsync(chapterMarker);
                    SetStatusMessage(
                        activated
                            ? $"Przejście do {chapterMarker.TimeLabel}."
                            : $"Nie udało się przejść do {chapterMarker.TimeLabel}."
                    );
                }
                break;
            case PodcastShowNotesSection.RelatedLinks when item is PodcastRelatedLinkItemViewModel relatedLink:
                await OpenRelatedLinkAsync(relatedLink);
                break;
        }
    }

    private void ToggleCommentDetails(CommentItemViewModel item)
    {
        var shouldExpand = !item.IsExpanded;
        foreach (var candidate in _comments)
        {
            candidate.IsExpanded = ReferenceEquals(candidate, item) && shouldExpand;
        }

        SetStatusMessage(
            shouldExpand
                ? $"Pokazano szczegóły komentarza: {item.AuthorName}."
                : $"Ukryto szczegóły komentarza: {item.AuthorName}."
        );
        RestoreSelection(item);
    }

    private void ShowChapterMarkerContextMenu(ListView listView, ContextRequestedEventArgs e)
    {
        var item =
            ItemContextResolver.Resolve<PodcastChapterMarkerItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as PodcastChapterMarkerItemViewModel;
        if (item is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var seekItem = new MenuFlyoutItem { Text = "Przejdź" };
        AutomationProperties.SetName(seekItem, $"Przejdź do {item.TimeLabel}");
        seekItem.Click += async (_, _) => await ActivateItemAsync(item);
        flyout.Items.Add(seekItem);

        var favoriteItem = new MenuFlyoutItem
        {
            Text = item.IsFavorite
                ? "Usuń z ulubionych (Ctrl+D)"
                : "Dodaj do ulubionych (Ctrl+D)",
        };
        AutomationProperties.SetName(favoriteItem, item.FavoriteMenuLabel);
        favoriteItem.Click += async (_, _) => await ToggleChapterMarkerFavoriteAsync(item);
        flyout.Items.Add(favoriteItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private void ShowRelatedLinkContextMenu(ListView listView, ContextRequestedEventArgs e)
    {
        var item =
            ItemContextResolver.Resolve<PodcastRelatedLinkItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as PodcastRelatedLinkItemViewModel;
        if (item is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var openItem = new MenuFlyoutItem { Text = "Otwórz odnośnik" };
        AutomationProperties.SetName(openItem, item.OpenMenuLabel);
        openItem.Click += async (_, _) => await OpenRelatedLinkAsync(item);
        flyout.Items.Add(openItem);

        var copyItem = new MenuFlyoutItem { Text = "Kopiuj odnośnik" };
        AutomationProperties.SetName(copyItem, item.CopyMenuLabel);
        copyItem.Click += async (_, _) => await CopyRelatedLinkAsync(item);
        flyout.Items.Add(copyItem);

        var shareItem = new MenuFlyoutItem { Text = "Udostępnij (Ctrl+U)" };
        AutomationProperties.SetName(shareItem, item.ShareMenuLabel);
        shareItem.Click += async (_, _) => await ShareRelatedLinkAsync(item);
        flyout.Items.Add(shareItem);

        var favoriteItem = new MenuFlyoutItem
        {
            Text = item.IsFavorite
                ? "Usuń z ulubionych (Ctrl+D)"
                : "Dodaj do ulubionych (Ctrl+D)",
        };
        AutomationProperties.SetName(favoriteItem, item.FavoriteMenuLabel);
        favoriteItem.Click += async (_, _) => await ToggleRelatedLinkFavoriteAsync(item);
        flyout.Items.Add(favoriteItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private async Task OpenRelatedLinkAsync(PodcastRelatedLinkItemViewModel item)
    {
        if (_openRelatedLinkAsync is null)
        {
            return;
        }

        bool opened;
        try
        {
            opened = await _openRelatedLinkAsync(item);
        }
        catch
        {
            opened = false;
        }

        SetStatusMessage(
            opened
                ? $"Otwarto odnośnik: {item.Title}."
                : $"Nie udało się otworzyć odnośnika: {item.Title}."
        );
        RestoreRelatedLinkSelection(item);
    }

    private async Task CopyRelatedLinkAsync(PodcastRelatedLinkItemViewModel item)
    {
        if (_copyRelatedLinkAsync is null)
        {
            return;
        }

        bool copied;
        try
        {
            copied = await _copyRelatedLinkAsync(item);
        }
        catch
        {
            copied = false;
        }

        SetStatusMessage(
            copied
                ? $"Skopiowano odnośnik: {item.Title}."
                : $"Nie udało się skopiować odnośnika: {item.Title}."
        );
        RestoreRelatedLinkSelection(item);
    }

    private async Task ShareRelatedLinkAsync(PodcastRelatedLinkItemViewModel item)
    {
        if (_shareRelatedLinkAsync is null)
        {
            return;
        }

        bool shared;
        try
        {
            shared = await _shareRelatedLinkAsync(item);
        }
        catch
        {
            shared = false;
        }

        SetStatusMessage(
            shared
                ? $"Otwarto systemowe udostępnianie dla: {item.Title}."
                : $"Nie udało się udostępnić odnośnika: {item.Title}."
        );
        RestoreRelatedLinkSelection(item);
    }

    private async Task ToggleChapterMarkerFavoriteAsync(PodcastChapterMarkerItemViewModel item)
    {
        if (_toggleChapterMarkerFavoriteAsync is null)
        {
            return;
        }

        var updated = item with { IsFavorite = !item.IsFavorite };
        bool success;
        try
        {
            success = await _toggleChapterMarkerFavoriteAsync(item);
        }
        catch
        {
            success = false;
        }

        if (!success)
        {
            SetStatusMessage("Nie udało się zaktualizować ulubionego tematu.", announce: true);
            return;
        }

        _chapterMarkers = _chapterMarkers
            .Select(candidate =>
                candidate.Equals(item) ? updated : candidate
            )
            .ToArray();
        ItemsList.ItemsSource = _chapterMarkers;
        SetStatusMessage(
            updated.IsFavorite
                ? $"Dodano temat do ulubionych: {item.Title}."
                : $"Usunięto temat z ulubionych: {item.Title}.",
            announce: true,
            important: true
        );
        RestoreSelection(updated);
    }

    private async Task ToggleRelatedLinkFavoriteAsync(PodcastRelatedLinkItemViewModel item)
    {
        if (_toggleRelatedLinkFavoriteAsync is null)
        {
            return;
        }

        var updated = item with { IsFavorite = !item.IsFavorite };
        bool success;
        try
        {
            success = await _toggleRelatedLinkFavoriteAsync(item);
        }
        catch
        {
            success = false;
        }

        if (!success)
        {
            SetStatusMessage("Nie udało się zaktualizować ulubionego odnośnika.", announce: true);
            return;
        }

        _relatedLinks = _relatedLinks
            .Select(candidate =>
                candidate.Equals(item) ? updated : candidate
            )
            .ToArray();
        ItemsList.ItemsSource = _relatedLinks;
        SetStatusMessage(
            updated.IsFavorite
                ? $"Dodano odnośnik do ulubionych: {item.Title}."
                : $"Usunięto odnośnik z ulubionych: {item.Title}.",
            announce: true,
            important: true
        );
        RestoreSelection(updated);
    }

    private void RestoreSelection(object item)
    {
        ItemsList.SelectedItem = item;
        ItemsList.ScrollIntoView(item);
        ItemsList.UpdateLayout();
        ItemsList.Focus(FocusState.Programmatic);
    }

    private void RestoreRelatedLinkSelection(PodcastRelatedLinkItemViewModel item)
    {
        var selectedItem = _relatedLinks.FirstOrDefault(candidate =>
            Uri.Compare(
                candidate.Url,
                item.Url,
                UriComponents.AbsoluteUri,
                UriFormat.SafeUnescaped,
                StringComparison.Ordinal
            ) == 0
        );
        if (selectedItem is null)
        {
            return;
        }

        RestoreSelection(selectedItem);
    }

    private void SetStatusMessage(string? message, bool announce = false, bool important = false)
    {
        StatusTextBlock.Text = message ?? string.Empty;
        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (announce)
        {
            AutomationAnnouncementHelper.Announce(StatusTextBlock, message, important);
        }
    }
}
