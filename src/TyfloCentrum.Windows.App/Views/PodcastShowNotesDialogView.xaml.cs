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
    private Func<PodcastRelatedLinkItemViewModel, Task<bool>>? _openRelatedLinkAsync;

    public PodcastShowNotesDialogView()
    {
        InitializeComponent();
    }

    public void InitializeComments(string podcastTitle, CommentItemViewModel[] comments)
    {
        _section = PodcastShowNotesSection.Comments;
        _comments = comments;
        _chapterMarkers = [];
        _relatedLinks = [];
        _activateChapterMarkerAsync = null;
        _openRelatedLinkAsync = null;
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
        string podcastTitle,
        PodcastChapterMarkerItemViewModel[] chapterMarkers,
        Func<PodcastChapterMarkerItemViewModel, Task<bool>> activateChapterMarkerAsync
    )
    {
        _section = PodcastShowNotesSection.ChapterMarkers;
        _comments = [];
        _chapterMarkers = chapterMarkers;
        _relatedLinks = [];
        _activateChapterMarkerAsync = activateChapterMarkerAsync;
        _openRelatedLinkAsync = null;
        PodcastTitleTextBlock.Text = podcastTitle;
        SectionHintTextBlock.Text =
            "Enter uruchamia odtwarzacz podcastu od zaznaczonego znacznika czasu.";
        AutomationProperties.SetName(ItemsList, "Znaczniki czasu podcastu");
        ItemsList.ItemTemplate = (DataTemplate)Resources["ChapterMarkerItemTemplate"];
        ItemsList.ItemsSource = _chapterMarkers;
        ItemsList.SelectedItem = _chapterMarkers.FirstOrDefault();
        SetStatusMessage(null);
    }

    public void InitializeRelatedLinks(
        string podcastTitle,
        PodcastRelatedLinkItemViewModel[] relatedLinks,
        Func<PodcastRelatedLinkItemViewModel, Task<bool>> openRelatedLinkAsync
    )
    {
        _section = PodcastShowNotesSection.RelatedLinks;
        _comments = [];
        _chapterMarkers = [];
        _relatedLinks = relatedLinks;
        _activateChapterMarkerAsync = null;
        _openRelatedLinkAsync = openRelatedLinkAsync;
        PodcastTitleTextBlock.Text = podcastTitle;
        SectionHintTextBlock.Text = "Enter otwiera zaznaczony odnośnik w przeglądarce.";
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
        if (e.Key != VirtualKey.Enter || ItemsList.SelectedItem is null)
        {
            return;
        }

        e.Handled = true;
        await ActivateItemAsync(ItemsList.SelectedItem);
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
                    await _activateChapterMarkerAsync(chapterMarker);
                }
                break;
            case PodcastShowNotesSection.RelatedLinks when item is PodcastRelatedLinkItemViewModel relatedLink:
                if (_openRelatedLinkAsync is not null)
                {
                    var opened = await _openRelatedLinkAsync(relatedLink);
                    SetStatusMessage(
                        opened
                            ? $"Otwarto odnośnik: {relatedLink.Title}."
                            : $"Nie udało się otworzyć odnośnika: {relatedLink.Title}."
                    );
                    RestoreSelection(relatedLink);
                }
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

    private void RestoreSelection(object item)
    {
        ItemsList.SelectedItem = item;
        ItemsList.ScrollIntoView(item);
        ItemsList.UpdateLayout();
        ItemsList.Focus(FocusState.Programmatic);
    }

    private void SetStatusMessage(string? message)
    {
        StatusTextBlock.Text = message ?? string.Empty;
        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
