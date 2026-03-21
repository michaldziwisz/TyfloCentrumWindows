using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class TyfloSwiatMagazineView : UserControl
{
    private readonly InAppBrowserDialogService _inAppBrowserDialogService;
    private readonly IShareService _shareService;
    private TyfloSwiatMagazineIssueItemViewModel? _pendingFocusedIssue;
    private bool _focusIssueContentWhenReady;
    private bool _restoreFocusToIssuesList;
    private bool _synchronizingIssueSelection;
    private bool _synchronizingYearSelection;

    public TyfloSwiatMagazineView(
        TyfloSwiatMagazineViewModel viewModel,
        InAppBrowserDialogService inAppBrowserDialogService,
        IShareService shareService
    )
    {
        ViewModel = viewModel;
        _inAppBrowserDialogService = inAppBrowserDialogService;
        _shareService = shareService;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Years.CollectionChanged += OnCollectionChanged;
        ViewModel.Issues.CollectionChanged += OnCollectionChanged;
        ViewModel.TocItems.CollectionChanged += OnCollectionChanged;
        UpdateVisualState();
    }

    public TyfloSwiatMagazineViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        if (ViewModel.HasYears && ViewModel.Years.Count > 0)
        {
            var selectedYear = YearsList.SelectedItem as TyfloSwiatMagazineYearItemViewModel
                ?? ViewModel.SelectedYear
                ?? ViewModel.Years[0];
            ListViewFocusHelper.RestoreFocus(YearsList, selectedYear);
            return;
        }

        YearsList.Focus(FocusState.Programmatic);
    }

    public bool HandleEscapeNavigation()
    {
        if (FocusNavigationHelper.IsFocusWithin(TocItemsList))
        {
            IssuesList.Focus(FocusState.Programmatic);
            return true;
        }

        if (
            FocusNavigationHelper.IsFocusWithin(IssueActionsPanel)
            || FocusNavigationHelper.IsFocusWithin(IssueDetailCard)
        )
        {
            FocusIssuesList();
            return true;
        }

        if (FocusNavigationHelper.IsFocusWithin(IssuesList))
        {
            FocusYearsList();
            return true;
        }

        return false;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadIfNeededAsync();
    }

    private void OnYearsListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingYearSelection)
        {
            return;
        }

        if (sender is ListView { SelectedItem: TyfloSwiatMagazineYearItemViewModel year })
        {
            ViewModel.SelectYearForNavigation(year);
        }
    }

    private async void OnYearsListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await OpenSelectedYearAndFocusIssuesAsync();
    }

    private void OnIssueSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingIssueSelection)
        {
            return;
        }

        if (sender is ListView { SelectedItem: TyfloSwiatMagazineIssueItemViewModel issue })
        {
            _pendingFocusedIssue = issue;
            _restoreFocusToIssuesList = FocusNavigationHelper.IsFocusWithin(IssuesList);
            ViewModel.SelectIssueForNavigation(issue);
            RestorePendingIssueFocus();
        }
    }

    private void OnIssuesListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        FocusIssueContent();
    }

    private void OnIssuesListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var issue =
            ItemContextResolver.Resolve<TyfloSwiatMagazineIssueItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as TyfloSwiatMagazineIssueItemViewModel;
        if (issue is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var browserItem = new MenuFlyoutItem { Text = "Otwórz numer w przeglądarce" };
        AutomationProperties.SetName(browserItem, issue.OpenLinkLabel);
        browserItem.Click += async (_, _) => await ViewModel.OpenSelectedIssueAsync();
        flyout.Items.Add(browserItem);

        if (ViewModel.CanOpenSelectedIssuePdf)
        {
            var pdfItem = new MenuFlyoutItem { Text = "Pobierz PDF" };
            AutomationProperties.SetName(pdfItem, $"Pobierz PDF numeru: {issue.Title}");
            pdfItem.Click += async (_, _) => await ViewModel.OpenPdfAsync();
            flyout.Items.Add(pdfItem);
        }

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private async void OnTocItemsListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TyfloSwiatMagazineTocItemViewModel item)
        {
            await OpenTocDetailsAsync(item);
        }
    }

    private async void OnTocItemsListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (sender is ListView { SelectedItem: TyfloSwiatMagazineTocItemViewModel item })
        {
            e.Handled = true;
            await OpenTocDetailsAsync(item);
        }
    }

    private void OnTocItemsListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var item =
            ItemContextResolver.Resolve<TyfloSwiatMagazineTocItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as TyfloSwiatMagazineTocItemViewModel;
        if (item is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var openItem = new MenuFlyoutItem { Text = "Otwórz artykuł" };
        AutomationProperties.SetName(openItem, item.OpenDetailsLabel);
        openItem.Click += async (_, _) => await OpenTocDetailsAsync(item);
        flyout.Items.Add(openItem);

        var browserItem = new MenuFlyoutItem { Text = "Otwórz w przeglądarce" };
        AutomationProperties.SetName(browserItem, item.OpenLinkLabel);
        browserItem.Click += async (_, _) => await ViewModel.OpenTocItemInBrowserAsync(item);
        flyout.Items.Add(browserItem);

        var shareItem = new MenuFlyoutItem { Text = "Udostępnij" };
        AutomationProperties.SetName(shareItem, $"Udostępnij artykuł: {item.Title}");
        shareItem.Click += async (_, _) =>
        {
            var shared = await _shareService.ShareLinkAsync(item.Title, null, item.Link);
            if (!shared)
            {
                await DialogHelpers.ShowErrorAsync(XamlRoot, "Nie udało się udostępnić artykułu.");
            }

            if (ViewModel.TocItems.Contains(item))
            {
                TocItemsList.SelectedItem = item;
                TocItemsList.ScrollIntoView(item);
                TocItemsList.Focus(FocusState.Programmatic);
            }
        };
        flyout.Items.Add(shareItem);

        var favoriteItem = new MenuFlyoutItem { Text = item.FavoriteButtonText };
        AutomationProperties.SetName(favoriteItem, item.FavoriteButtonLabel);
        favoriteItem.Click += async (_, _) => await ViewModel.ToggleTocFavoriteAsync(item);
        flyout.Items.Add(favoriteItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (FocusNavigationHelper.IsFocusWithin(YearsList))
            {
                e.Handled = true;
                _ = OpenSelectedYearAndFocusIssuesAsync();
                return;
            }

            if (FocusNavigationHelper.IsFocusWithin(IssuesList))
            {
                e.Handled = true;
                FocusIssueContent();
                return;
            }

            if (
                FocusNavigationHelper.IsFocusWithin(TocItemsList)
                && TocItemsList.SelectedItem is TyfloSwiatMagazineTocItemViewModel tocItem
            )
            {
                e.Handled = true;
                _ = OpenTocDetailsAsync(tocItem);
                return;
            }
        }

        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        if (HandleEscapeNavigation())
        {
            e.Handled = true;
        }
    }

    private async Task OpenTocDetailsAsync(TyfloSwiatMagazineTocItemViewModel item)
    {
        AutomationAnnouncementHelper.Announce(
            TocItemsList,
            $"Otwieranie artykułu: {item.Title}.",
            important: true
        );
        await _inAppBrowserDialogService.ShowTyfloSwiatPageAsync(
            item.PageId,
            item.Title,
            item.PublishedDate,
            item.Link,
            XamlRoot
        );
        await ViewModel.RefreshTocFavoriteAsync(item);
        if (ViewModel.TocItems.Contains(item))
        {
            TocItemsList.SelectedItem = item;
            TocItemsList.ScrollIntoView(item);
            TocItemsList.Focus(FocusState.Programmatic);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        LoadingIndicator.IsActive = ViewModel.IsLoading;
        LoadingIndicator.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        IssueLoadingIndicator.IsActive = ViewModel.IsIssueLoading;
        IssueLoadingIndicator.Visibility = ViewModel.IsIssueLoading
            ? Visibility.Visible
            : Visibility.Collapsed;

        ErrorPanel.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.IsOpen = ViewModel.HasError;
        ErrorBar.Message = ViewModel.ErrorMessage;

        IssueErrorPanel.Visibility = ViewModel.HasIssueError ? Visibility.Visible : Visibility.Collapsed;
        IssueErrorBar.IsOpen = ViewModel.HasIssueError;
        IssueErrorBar.Message = ViewModel.IssueErrorMessage;

        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.StatusMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

        YearsEmptyStateText.Visibility = ViewModel.HasYears ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Visibility = ViewModel.ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        IssuesList.Visibility = ViewModel.HasIssues ? Visibility.Visible : Visibility.Collapsed;

        IssueTitleTextBlock.Visibility = ViewModel.HasIssueDetail ? Visibility.Visible : Visibility.Collapsed;
        IssueDateTextBlock.Visibility = ViewModel.HasIssueDetail ? Visibility.Visible : Visibility.Collapsed;
        IssueActionsPanel.Visibility = Visibility.Collapsed;

        TocSection.Visibility = ViewModel.HasTocItems ? Visibility.Visible : Visibility.Collapsed;
        IssueContentTextBlock.Visibility = ViewModel.HasIssueContent && !ViewModel.HasTocItems
            ? Visibility.Visible
            : Visibility.Collapsed;
        IssuePlaceholderText.Visibility = ViewModel.ShowIssuePlaceholder
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!ReferenceEquals(IssuesList.SelectedItem, ViewModel.SelectedIssue))
        {
            _synchronizingIssueSelection = true;
            IssuesList.SelectedItem = ViewModel.SelectedIssue;
            _synchronizingIssueSelection = false;
        }

        if (!ReferenceEquals(YearsList.SelectedItem, ViewModel.SelectedYear))
        {
            _synchronizingYearSelection = true;
            YearsList.SelectedItem = ViewModel.SelectedYear;
            _synchronizingYearSelection = false;
        }

        RestorePendingIssueFocus();

        if (_focusIssueContentWhenReady && !ViewModel.IsIssueLoading)
        {
            _focusIssueContentWhenReady = false;
            FocusIssueContent();
        }
    }

    private void FocusYearsList()
    {
        if (ViewModel.HasYears && ViewModel.Years.Count > 0)
        {
            var selectedYear = YearsList.SelectedItem as TyfloSwiatMagazineYearItemViewModel
                ?? ViewModel.SelectedYear
                ?? ViewModel.Years[0];
            ListViewFocusHelper.RestoreFocus(YearsList, selectedYear);
            return;
        }

        YearsList.Focus(FocusState.Programmatic);
    }

    private void FocusIssuesList()
    {
        if (ViewModel.HasIssues && ViewModel.Issues.Count > 0)
        {
            var selectedIssue = IssuesList.SelectedItem as TyfloSwiatMagazineIssueItemViewModel
                ?? ViewModel.SelectedIssue
                ?? ViewModel.Issues[0];
            ListViewFocusHelper.RestoreFocus(IssuesList, selectedIssue);
            return;
        }

        IssuesList.Focus(FocusState.Programmatic);
    }

    private async Task OpenSelectedYearAndFocusIssuesAsync()
    {
        await ViewModel.OpenSelectedYearAsync(ViewModel.SelectedYear);
        FocusIssuesList();
    }

    private void FocusIssueContent()
    {
        if (ViewModel.IsIssueLoading)
        {
            _focusIssueContentWhenReady = true;
            return;
        }

        if (
            ViewModel.SelectedIssue is null
            && ViewModel.HasIssues
            && ViewModel.Issues.Count > 0
        )
        {
            ListViewFocusHelper.RestoreFocus(IssuesList, ViewModel.Issues[0]);
            return;
        }

        if (
            ViewModel.SelectedIssue is TyfloSwiatMagazineIssueItemViewModel selectedIssue
            && !ViewModel.HasTocItems
            && !ViewModel.HasIssueContent
            && !ViewModel.HasIssueError
        )
        {
            _focusIssueContentWhenReady = true;
            AutomationAnnouncementHelper.Announce(
                IssuesList,
                $"Otwieranie numeru: {selectedIssue.Title}.",
                important: true
            );
            _ = ViewModel.SelectIssueAsync(selectedIssue);
            return;
        }

        if (ViewModel.HasTocItems && ViewModel.TocItems.Count > 0)
        {
            var selectedItem = TocItemsList.SelectedItem as TyfloSwiatMagazineTocItemViewModel
                ?? ViewModel.TocItems[0];
            ListViewFocusHelper.RestoreFocus(TocItemsList, selectedItem);
            return;
        }

        FocusIssuesList();
    }

    private void RestorePendingIssueFocus()
    {
        if (ViewModel.IsIssueLoading || _pendingFocusedIssue is null)
        {
            return;
        }

        if (!ReferenceEquals(_pendingFocusedIssue, ViewModel.SelectedIssue))
        {
            _pendingFocusedIssue = null;
            _restoreFocusToIssuesList = false;
            return;
        }

        _synchronizingIssueSelection = true;
        IssuesList.SelectedItem = _pendingFocusedIssue;
        _synchronizingIssueSelection = false;
        IssuesList.ScrollIntoView(_pendingFocusedIssue);
        IssuesList.UpdateLayout();

        _pendingFocusedIssue = null;
        _restoreFocusToIssuesList = false;
    }
}
