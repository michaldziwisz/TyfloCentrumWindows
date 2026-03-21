using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class FavoritesSectionView : UserControl
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly IAudioPlaybackRequestFactory _audioPlaybackRequestFactory;
    private readonly InAppBrowserDialogService _inAppBrowserDialogService;
    private readonly PostDetailDialogService _postDetailDialogService;

    public event EventHandler? ExitToSectionListRequested;

    public FavoritesSectionView(
        FavoritesViewModel viewModel,
        PostDetailDialogService postDetailDialogService,
        InAppBrowserDialogService inAppBrowserDialogService,
        AudioPlayerDialogService audioPlayerDialogService,
        IAudioPlaybackRequestFactory audioPlaybackRequestFactory
    )
    {
        ViewModel = viewModel;
        _postDetailDialogService = postDetailDialogService;
        _inAppBrowserDialogService = inAppBrowserDialogService;
        _audioPlayerDialogService = audioPlayerDialogService;
        _audioPlaybackRequestFactory = audioPlaybackRequestFactory;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
        UpdateVisualState();
    }

    public FavoritesViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        FilterComboBox.Focus(FocusState.Programmatic);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadIfNeededAsync();
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        return ViewModel.ReloadAsync(cancellationToken);
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ReloadAsync();
    }

    private async void OnFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: FavoriteFilterOptionViewModel filter })
        {
            await ViewModel.SelectFilterAsync(filter);
        }
    }

    private void OnFilterComboBoxKeyDown(
        object sender,
        Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e
    )
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;
        ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnOpenItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FavoriteItemViewModel item })
        {
            await ViewModel.OpenItemAsync(item);
        }
    }

    private async void OnCopyLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FavoriteItemViewModel item })
        {
            await ViewModel.CopyLinkAsync(item);
        }
    }

    private async void OnShareLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FavoriteItemViewModel item })
        {
            await ViewModel.ShareItemAsync(item);
        }
    }

    private async void OnItemsListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FavoriteItemViewModel item)
        {
            await OpenDefaultActionAsync(item);
        }
    }

    private async void OnItemsListKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (sender is ListView { SelectedItem: FavoriteItemViewModel item })
        {
            e.Handled = true;
            await OpenDefaultActionAsync(item);
        }
    }

    private async void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FavoriteItemViewModel item })
        {
            await ViewModel.RemoveAsync(item);
        }
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

        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.StatusMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;
        EmptyStateText.Visibility = ViewModel.ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        ItemsList.Visibility = ViewModel.HasItems ? Visibility.Visible : Visibility.Collapsed;
        FilterComboBox.IsEnabled = !ViewModel.IsLoading;
        ItemsList.IsEnabled = !ViewModel.IsLoading;
    }

    private async Task OpenDefaultActionAsync(FavoriteItemViewModel item)
    {
        switch (item.Kind)
        {
            case FavoriteKind.Podcast:
                await _postDetailDialogService.ShowAsync(
                    ContentSource.Podcast,
                    item.PostId,
                    item.Title,
                    item.PublishedDate,
                    item.Link,
                    XamlRoot
                );
                await ViewModel.ReloadAsync();
                RestoreItemFocus(item.Id);
                break;
            case FavoriteKind.Article when item.ArticleOrigin == FavoriteArticleOrigin.Page:
                await _inAppBrowserDialogService.ShowTyfloSwiatPageAsync(
                    item.PostId,
                    item.Title,
                    item.PublishedDate,
                    item.Link,
                    XamlRoot
                );
                await ViewModel.ReloadAsync();
                RestoreItemFocus(item.Id);
                break;
            case FavoriteKind.Article:
                await _inAppBrowserDialogService.ShowAsync(
                    ContentSource.Article,
                    item.PostId,
                    item.Title,
                    item.PublishedDate,
                    item.Link,
                    XamlRoot
                );
                await ViewModel.ReloadAsync();
                RestoreItemFocus(item.Id);
                break;
            case FavoriteKind.Topic:
            {
                var request = _audioPlaybackRequestFactory.CreatePodcast(
                    item.PostId,
                    string.IsNullOrWhiteSpace(item.ContextTitle) ? item.Subtitle : item.ContextTitle,
                    string.IsNullOrWhiteSpace(item.ContextSubtitle) ? item.PublishedDate : item.ContextSubtitle,
                    item.StartPositionSeconds
                );
                await _audioPlayerDialogService.ShowAsync(request, XamlRoot);
                await ViewModel.ReloadAsync();
                RestoreItemFocus(item.Id);
                break;
            }
            case FavoriteKind.Link:
                await ViewModel.OpenItemAsync(item);
                RestoreItemFocus(item.Id);
                break;
        }
    }

    private void RestoreItemFocus(string itemId)
    {
        var restoredItem = ViewModel.Items.FirstOrDefault(item =>
            string.Equals(item.Id, itemId, StringComparison.Ordinal)
        );

        if (restoredItem is not null)
        {
            ListViewFocusHelper.RestoreFocus(ItemsList, restoredItem);
            return;
        }

        if (ViewModel.Items.Count > 0)
        {
            ListViewFocusHelper.RestoreFocus(ItemsList, ViewModel.Items[0]);
            return;
        }

        FilterComboBox.Focus(FocusState.Programmatic);
    }

    private void OnPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || !FocusNavigationHelper.IsFocusWithin(this))
        {
            return;
        }

        e.Handled = true;
        ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
    }
}
