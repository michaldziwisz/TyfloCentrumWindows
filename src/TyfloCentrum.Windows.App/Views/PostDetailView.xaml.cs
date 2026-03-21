using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class PostDetailView : UserControl
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly CommentDetailDialogService _commentDetailDialogService;

    public PostDetailView(
        PostDetailViewModel viewModel,
        AudioPlayerDialogService audioPlayerDialogService,
        CommentDetailDialogService commentDetailDialogService
    )
    {
        _audioPlayerDialogService = audioPlayerDialogService;
        _commentDetailDialogService = commentDetailDialogService;
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Comments.CollectionChanged += OnCommentsCollectionChanged;
        UpdateVisualState();
    }

    public PostDetailViewModel ViewModel { get; }

    public Func<Task>? BrowserRequestHandler { get; set; }

    public Func<Task>? ShareRequestHandler { get; set; }

    public Func<AudioPlaybackRequest, Task>? PlaybackRequestHandler { get; set; }

    public Func<CommentItemViewModel, Task>? CommentDetailRequestHandler { get; set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadIfNeededAsync();
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryAsync();
    }

    private async void OnOpenInBrowserClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Source == ContentSource.Article && BrowserRequestHandler is not null)
        {
            await BrowserRequestHandler();
            return;
        }

        await ViewModel.OpenInBrowserAsync();
    }

    private async void OnShareClick(object sender, RoutedEventArgs e)
    {
        if (ShareRequestHandler is not null)
        {
            await ShareRequestHandler();
            return;
        }

        await ViewModel.ShareAsync();
    }

    private async void OnToggleFavoriteClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleFavoriteAsync();
    }

    private async void OnOpenPlayerClick(object sender, RoutedEventArgs e)
    {
        var request = ViewModel.CreatePlaybackRequest();
        if (request is null)
        {
            return;
        }

        if (PlaybackRequestHandler is not null)
        {
            await PlaybackRequestHandler(request);
            return;
        }

        var shown = await _audioPlayerDialogService.ShowAsync(request, XamlRoot);
        if (!shown)
        {
            ViewModel.ReportPlaybackError();
        }
    }

    private async void OnRetryCommentsClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryCommentsAsync();
    }

    private async void OnOpenCommentDetailsClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommentItemViewModel item })
        {
            if (CommentDetailRequestHandler is not null)
            {
                await CommentDetailRequestHandler(item);
                return;
            }

            await _commentDetailDialogService.ShowAsync(item, XamlRoot);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void OnCommentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        ListenButton.Visibility = ViewModel.CanListen ? Visibility.Visible : Visibility.Collapsed;

        ContentTextBlock.Visibility = ViewModel.HasContent ? Visibility.Visible : Visibility.Collapsed;
        EmptyContentText.Visibility = ViewModel.HasLoaded && !ViewModel.HasContent && !ViewModel.HasError
            ? Visibility.Visible
            : Visibility.Collapsed;

        CommentsSection.Visibility = ViewModel.SupportsComments ? Visibility.Visible : Visibility.Collapsed;
        CommentsLoadingIndicator.IsActive = ViewModel.IsCommentsLoading;
        CommentsLoadingIndicator.Visibility = ViewModel.IsCommentsLoading ? Visibility.Visible : Visibility.Collapsed;

        CommentsErrorPanel.Visibility = ViewModel.HasCommentsError ? Visibility.Visible : Visibility.Collapsed;
        CommentsErrorBar.IsOpen = ViewModel.HasCommentsError;
        CommentsErrorBar.Message = ViewModel.CommentsErrorMessage;

        CommentsEmptyText.Visibility = ViewModel.ShowCommentsEmptyState ? Visibility.Visible : Visibility.Collapsed;
        CommentsItemsControl.Visibility = ViewModel.HasComments ? Visibility.Visible : Visibility.Collapsed;
        CommentsItemsControl.ItemsSource = ViewModel.Comments;
    }
}
