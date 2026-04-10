using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class PodcastShowNotesDialogView : UserControl
{
    private readonly PodcastCommentComposerViewModel _commentComposerViewModel;
    private bool _isSynchronizingCommentComposer;
    private TextBlock? _commentComposerHeadingTextBlock;
    private TextBlock? _replyTargetTextBlock;
    private Button? _cancelReplyButton;
    private TextBox? _authorNameTextBox;
    private TextBox? _authorEmailTextBox;
    private TextBox? _commentContentTextBox;
    private Button? _submitCommentButton;
    private PodcastShowNotesSection _section;
    private CommentItemViewModel[] _comments = [];
    private PodcastChapterMarkerItemViewModel[] _chapterMarkers = [];
    private PodcastRelatedLinkItemViewModel[] _relatedLinks = [];
    private Func<CancellationToken, Task<CommentItemViewModel[]>>? _reloadCommentsAsync;
    private Func<PodcastChapterMarkerItemViewModel, Task<bool>>? _activateChapterMarkerAsync;
    private Func<PodcastChapterMarkerItemViewModel, Task<bool>>? _toggleChapterMarkerFavoriteAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _copyRelatedLinkAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _openRelatedLinkAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _shareRelatedLinkAsync;
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _toggleRelatedLinkFavoriteAsync;

    public PodcastShowNotesDialogView(PodcastCommentComposerViewModel commentComposerViewModel)
    {
        _commentComposerViewModel = commentComposerViewModel;
        InitializeComponent();
        BuildCommentComposerUi();
        _commentComposerViewModel.PropertyChanged += OnCommentComposerPropertyChanged;
        Loaded += OnLoaded;
    }

    public void InitializeComments(
        int podcastPostId,
        string podcastTitle,
        string podcastSubtitle,
        CommentItemViewModel[] comments,
        Func<CancellationToken, Task<CommentItemViewModel[]>> reloadCommentsAsync
    )
    {
        _section = PodcastShowNotesSection.Comments;
        _comments = comments;
        _chapterMarkers = [];
        _relatedLinks = [];
        _reloadCommentsAsync = reloadCommentsAsync;
        _activateChapterMarkerAsync = null;
        _toggleChapterMarkerFavoriteAsync = null;
        _copyRelatedLinkAsync = null;
        _openRelatedLinkAsync = null;
        _shareRelatedLinkAsync = null;
        _toggleRelatedLinkFavoriteAsync = null;
        _commentComposerViewModel.Initialize(podcastPostId);
        _commentComposerViewModel.CancelReply();
        ShowCommentComposerButton.Visibility = Visibility.Visible;
        CommentComposerPanel.Visibility = Visibility.Collapsed;
        UpdateCommentComposerUi();
        PodcastTitleTextBlock.Text = podcastTitle;
        SectionHintTextBlock.Text =
            comments.Length == 0
                ? "Brak komentarzy. Możesz dodać pierwszy komentarz albo odpowiedzieć później, gdy pojawią się wpisy."
                : "Enter rozwija albo zwija pełną treść zaznaczonego komentarza. Użyj przycisku Odpowiedz, aby odpowiedzieć na wybrany komentarz.";
        AutomationProperties.SetName(ItemsList, "Komentarze podcastu");
        ItemsList.ItemTemplate = (DataTemplate)Resources["CommentItemTemplate"];
        ItemsList.ItemsSource = _comments;
        ItemsList.SelectedItem = _comments.FirstOrDefault();
        SetStatusMessage(null);
    }

    public void BeginReplyToComment(int commentId)
    {
        var item = _comments.FirstOrDefault(candidate => candidate.Id == commentId);
        if (item is null)
        {
            return;
        }

        BeginReply(item);
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
        _reloadCommentsAsync = null;
        _activateChapterMarkerAsync = activateChapterMarkerAsync;
        _toggleChapterMarkerFavoriteAsync = toggleChapterMarkerFavoriteAsync;
        _copyRelatedLinkAsync = null;
        _openRelatedLinkAsync = null;
        _shareRelatedLinkAsync = null;
        _toggleRelatedLinkFavoriteAsync = null;
        _commentComposerViewModel.CancelReply();
        ShowCommentComposerButton.Visibility = Visibility.Collapsed;
        CommentComposerPanel.Visibility = Visibility.Collapsed;
        UpdateCommentComposerUi();
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
        _reloadCommentsAsync = null;
        _activateChapterMarkerAsync = null;
        _toggleChapterMarkerFavoriteAsync = null;
        _copyRelatedLinkAsync = copyRelatedLinkAsync;
        _openRelatedLinkAsync = openRelatedLinkAsync;
        _shareRelatedLinkAsync = shareRelatedLinkAsync;
        _toggleRelatedLinkFavoriteAsync = toggleRelatedLinkFavoriteAsync;
        _commentComposerViewModel.CancelReply();
        ShowCommentComposerButton.Visibility = Visibility.Collapsed;
        CommentComposerPanel.Visibility = Visibility.Collapsed;
        UpdateCommentComposerUi();
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
            case PodcastShowNotesSection.Comments:
                ShowCommentContextMenu(listView, e);
                break;
            case PodcastShowNotesSection.ChapterMarkers:
                ShowChapterMarkerContextMenu(listView, e);
                break;
            case PodcastShowNotesSection.RelatedLinks:
                ShowRelatedLinkContextMenu(listView, e);
                break;
        }
    }

    private void OnCommentReplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommentItemViewModel item })
        {
            BeginReply(item);
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

    private void ShowCommentContextMenu(ListView listView, ContextRequestedEventArgs e)
    {
        var item =
            ItemContextResolver.Resolve<CommentItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as CommentItemViewModel;
        if (item is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var replyItem = new MenuFlyoutItem { Text = item.ReplyButtonText };
        AutomationProperties.SetName(replyItem, item.ReplyButtonLabel);
        replyItem.Click += (_, _) => BeginReply(item);
        flyout.Items.Add(replyItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _commentComposerViewModel.LoadIfNeededAsync();
        UpdateCommentComposerUi();

        if (_section != PodcastShowNotesSection.Comments)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_comments.Length > 0)
            {
                if (ItemsList.SelectedItem is null)
                {
                    ItemsList.SelectedItem = _comments.FirstOrDefault();
                }

                ItemsList.Focus(FocusState.Programmatic);
                return;
            }

            ShowCommentComposerButton.Focus(FocusState.Programmatic);
        });
    }

    private void OnShowCommentComposerClick(object sender, RoutedEventArgs e)
    {
        _commentComposerViewModel.CancelReply();
        CommentComposerPanel.Visibility = Visibility.Visible;
        UpdateCommentComposerUi();
        SetStatusMessage("Dodawanie nowego komentarza.", announce: true);
        FocusCommentFormField(PodcastCommentFormField.AuthorName);
    }

    private void BeginReply(CommentItemViewModel item)
    {
        _commentComposerViewModel.BeginReply(item);
        CommentComposerPanel.Visibility = Visibility.Visible;
        UpdateCommentComposerUi();
        SetStatusMessage(
            $"Odpowiadasz na komentarz autora: {item.AuthorName}.",
            announce: true
        );
        FocusCommentFormField(PodcastCommentFormField.AuthorName);
    }

    private void OnCancelReplyClick(object sender, RoutedEventArgs e)
    {
        _commentComposerViewModel.CancelReply();
        UpdateCommentComposerUi();
        SetStatusMessage("Anulowano odpowiadanie na komentarz.", announce: true);
        _commentContentTextBox?.Focus(FocusState.Programmatic);
    }

    private async void OnSubmitCommentClick(object sender, RoutedEventArgs e)
    {
        await Task.Yield();
        var result = await _commentComposerViewModel.SubmitAsync();
        SetStatusMessage(result.Message, announce: true, important: !result.Accepted);

        if (!result.Accepted)
        {
            FocusCommentFormField(result.FocusTarget);
            return;
        }

        if (_reloadCommentsAsync is not null)
        {
            var previousSelectedCommentId = (ItemsList.SelectedItem as CommentItemViewModel)?.Id;
            _comments = await _reloadCommentsAsync(CancellationToken.None);
            ItemsList.ItemsSource = _comments;
            ItemsList.SelectedItem =
                _comments.FirstOrDefault(candidate => candidate.Id == previousSelectedCommentId)
                ?? _comments.LastOrDefault();
        }

        CommentComposerPanel.Visibility = Visibility.Collapsed;
        SectionHintTextBlock.Text =
            _comments.Length == 0
                ? "Brak komentarzy. Możesz dodać pierwszy komentarz albo odpowiedzieć później, gdy pojawią się wpisy."
                : "Enter rozwija albo zwija pełną treść zaznaczonego komentarza. Użyj przycisku Odpowiedz, aby odpowiedzieć na wybrany komentarz.";
        UpdateCommentComposerUi();
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_comments.Length > 0)
            {
                ItemsList.Focus(FocusState.Programmatic);
                return;
            }

            ShowCommentComposerButton.Focus(FocusState.Programmatic);
        });
    }

    private void FocusCommentFormField(PodcastCommentFormField field)
    {
        switch (field)
        {
            case PodcastCommentFormField.AuthorName:
                _authorNameTextBox?.Focus(FocusState.Programmatic);
                break;
            case PodcastCommentFormField.AuthorEmail:
                _authorEmailTextBox?.Focus(FocusState.Programmatic);
                break;
            case PodcastCommentFormField.Content:
                _commentContentTextBox?.Focus(FocusState.Programmatic);
                break;
        }
    }

    private void BuildCommentComposerUi()
    {
        _commentComposerHeadingTextBlock = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };

        _replyTargetTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords,
        };

        _cancelReplyButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = "Anuluj odpowiedź",
        };
        _cancelReplyButton.Click += OnCancelReplyClick;

        _authorNameTextBox = new TextBox
        {
            Header = "Imię *",
        };
        _authorNameTextBox.TextChanged += OnAuthorNameTextChanged;

        _authorEmailTextBox = new TextBox
        {
            Header = "Adres e-mail *",
            InputScope = new InputScope
            {
                Names = { new InputScopeName(InputScopeNameValue.EmailSmtpAddress) },
            },
        };
        _authorEmailTextBox.TextChanged += OnAuthorEmailTextChanged;

        _commentContentTextBox = new TextBox
        {
            Header = "Treść komentarza *",
            AcceptsReturn = true,
            MinHeight = 120,
            TextWrapping = TextWrapping.Wrap,
        };
        _commentContentTextBox.TextChanged += OnCommentContentTextChanged;

        _submitCommentButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _submitCommentButton.Click += OnSubmitCommentClick;

        CommentComposerHost.Children.Clear();
        CommentComposerHost.Children.Add(_commentComposerHeadingTextBlock);
        CommentComposerHost.Children.Add(new TextBlock { Text = "Pola oznaczone * są obowiązkowe." });
        CommentComposerHost.Children.Add(_replyTargetTextBlock);
        CommentComposerHost.Children.Add(_cancelReplyButton);
        CommentComposerHost.Children.Add(_authorNameTextBox);
        CommentComposerHost.Children.Add(_authorEmailTextBox);
        CommentComposerHost.Children.Add(_commentContentTextBox);
        CommentComposerHost.Children.Add(_submitCommentButton);
        UpdateCommentComposerUi();
    }

    private void OnCommentComposerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateCommentComposerUi();
    }

    private void UpdateCommentComposerUi()
    {
        if (
            _commentComposerHeadingTextBlock is null
            || _replyTargetTextBlock is null
            || _cancelReplyButton is null
            || _authorNameTextBox is null
            || _authorEmailTextBox is null
            || _commentContentTextBox is null
            || _submitCommentButton is null
        )
        {
            return;
        }

        _isSynchronizingCommentComposer = true;
        try
        {
            _commentComposerHeadingTextBlock.Text = _commentComposerViewModel.FormHeadingText;
            _replyTargetTextBlock.Text = _commentComposerViewModel.ReplyTargetText;
            _replyTargetTextBlock.Visibility = _commentComposerViewModel.HasReplyTarget
                ? Visibility.Visible
                : Visibility.Collapsed;
            _cancelReplyButton.Visibility = _commentComposerViewModel.HasReplyTarget
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (!string.Equals(_authorNameTextBox.Text, _commentComposerViewModel.AuthorName, StringComparison.Ordinal))
            {
                _authorNameTextBox.Text = _commentComposerViewModel.AuthorName;
            }

            if (!string.Equals(_authorEmailTextBox.Text, _commentComposerViewModel.AuthorEmail, StringComparison.Ordinal))
            {
                _authorEmailTextBox.Text = _commentComposerViewModel.AuthorEmail;
            }

            if (!string.Equals(_commentContentTextBox.Text, _commentComposerViewModel.Content, StringComparison.Ordinal))
            {
                _commentContentTextBox.Text = _commentComposerViewModel.Content;
            }

            var controlsEnabled = _commentComposerViewModel.CanSubmit;
            _authorNameTextBox.IsEnabled = controlsEnabled;
            _authorEmailTextBox.IsEnabled = controlsEnabled;
            _commentContentTextBox.IsEnabled = controlsEnabled;
            _submitCommentButton.IsEnabled = controlsEnabled;
            _submitCommentButton.Content = _commentComposerViewModel.SubmitButtonText;
            AutomationProperties.SetName(_submitCommentButton, _commentComposerViewModel.SubmitButtonText);
        }
        finally
        {
            _isSynchronizingCommentComposer = false;
        }
    }

    private void OnAuthorNameTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizingCommentComposer || _authorNameTextBox is null)
        {
            return;
        }

        _commentComposerViewModel.AuthorName = _authorNameTextBox.Text;
    }

    private void OnAuthorEmailTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizingCommentComposer || _authorEmailTextBox is null)
        {
            return;
        }

        _commentComposerViewModel.AuthorEmail = _authorEmailTextBox.Text;
    }

    private void OnCommentContentTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizingCommentComposer || _commentContentTextBox is null)
        {
            return;
        }

        _commentComposerViewModel.Content = _commentContentTextBox.Text;
    }
}
