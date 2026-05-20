using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class RadioSectionView : UserControl
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly ContactTextMessageDialogService _contactTextMessageDialogService;
    private readonly ContactVoiceMessageDialogService _contactVoiceMessageDialogService;
    private string? _lastAnnouncedStatusMessage;
    private string? _lastRenderedScheduleHtmlDocument;

    public event EventHandler? ExitToSectionListRequested;

    public RadioSectionView(
        RadioViewModel viewModel,
        AudioPlayerDialogService audioPlayerDialogService,
        ContactTextMessageDialogService contactTextMessageDialogService,
        ContactVoiceMessageDialogService contactVoiceMessageDialogService
    )
    {
        _audioPlayerDialogService = audioPlayerDialogService;
        _contactTextMessageDialogService = contactTextMessageDialogService;
        _contactVoiceMessageDialogService = contactVoiceMessageDialogService;
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisualState();
    }

    public RadioViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        ListenButton.Focus(FocusState.Programmatic);
    }

    public async Task FocusScheduleContentAsync()
    {
        await ViewModel.LoadIfNeededAsync();
        await ShowScheduleAsync();
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

        UpdateLayout();
        await Task.Delay(200);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadIfNeededAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private async void OnOpenStreamClick(object sender, RoutedEventArgs e)
    {
        var shown = await _audioPlayerDialogService.ShowAsync(
            ViewModel.CreatePlaybackRequest(),
            XamlRoot
        );

        if (!shown)
        {
            ViewModel.ReportPlaybackError();
        }
    }

    private async void OnOpenContactClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryStartTextContact())
        {
            return;
        }

        var dialogXamlRoot = (sender as FrameworkElement)?.XamlRoot ?? XamlRoot;
        var result = await _contactTextMessageDialogService.ShowAsync(dialogXamlRoot);
        if (result == FormDialogResult.Submitted)
        {
            ViewModel.ReportContactSent();
            return;
        }

        if (result == FormDialogResult.FailedToOpen)
        {
            ViewModel.ReportContactFormOpenError();
        }
    }

    private async void OnOpenVoiceContactClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryStartVoiceContact())
        {
            return;
        }

        var dialogXamlRoot = (sender as FrameworkElement)?.XamlRoot ?? XamlRoot;
        var result = await _contactVoiceMessageDialogService.ShowAsync(dialogXamlRoot);
        if (result == FormDialogResult.Submitted)
        {
            ViewModel.ReportVoiceMessageSent();
            return;
        }

        if (result == FormDialogResult.FailedToOpen)
        {
            ViewModel.ReportVoiceContactFormOpenError();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RadioViewModel.StatusAnnouncement))
        {
            AnnounceStatusMessage(ViewModel.StatusAnnouncement);
        }

        UpdateVisualState();

        if (
            e.PropertyName is nameof(RadioViewModel.ScheduleHtmlDocument)
                or nameof(RadioViewModel.ScheduleDisplayText)
            && (ScheduleBrowser.Visibility == Visibility.Visible || ScheduleEditor.Visibility == Visibility.Visible)
        )
        {
            _ = ShowScheduleAsync();
        }
    }

    private void UpdateVisualState()
    {
        LoadingIndicator.IsActive = ViewModel.IsLoading;
        LoadingIndicator.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;

        ErrorPanel.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.IsOpen = ViewModel.HasError;
        ErrorBar.Message = ViewModel.ErrorMessage;

        AnnouncementTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.StatusAnnouncement)
            ? Visibility.Collapsed
            : Visibility.Visible;

        RefreshButton.IsEnabled = !ViewModel.IsLoading;
        ListenButton.IsEnabled = !ViewModel.IsLoading;
        ContactButton.IsEnabled = ViewModel.CanOpenContact;
        VoiceContactButton.IsEnabled = ViewModel.CanOpenContact;
        ScheduleButton.IsEnabled = ViewModel.CanOpenSchedule;
    }

    private void AnnounceStatusMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _lastAnnouncedStatusMessage = null;
            return;
        }

        if (string.Equals(_lastAnnouncedStatusMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastAnnouncedStatusMessage = message;
        AutomationAnnouncementHelper.Announce(AnnouncementTextBlock, message, important: true);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || !FocusNavigationHelper.IsFocusWithin(this))
        {
            return;
        }

        e.Handled = true;
        ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnOpenScheduleClick(object sender, RoutedEventArgs e)
    {
        await ShowScheduleAsync();
    }

    private async Task ShowScheduleAsync()
    {
        if (!ViewModel.HasScheduleText)
        {
            ScheduleBrowser.Visibility = Visibility.Collapsed;
            ScheduleEditor.Visibility = Visibility.Visible;
            ScheduleEditor.UpdateLayout();
            await Task.Yield();
            ScheduleEditor.StartBringIntoView();
            ScheduleEditor.Focus(FocusState.Programmatic);
            ScheduleEditor.Select(0, 0);
            return;
        }

        ScheduleEditor.Visibility = Visibility.Collapsed;
        ScheduleBrowser.Visibility = Visibility.Visible;
        ScheduleBrowser.UpdateLayout();

        try
        {
            await ScheduleBrowser.EnsureCoreWebView2Async();
            if (ScheduleBrowser.CoreWebView2 is not null)
            {
                ScheduleBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                ScheduleBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            }

            if (
                !string.Equals(
                    _lastRenderedScheduleHtmlDocument,
                    ViewModel.ScheduleHtmlDocument,
                    StringComparison.Ordinal
                )
            )
            {
                ScheduleBrowser.NavigateToString(ViewModel.ScheduleHtmlDocument);
                _lastRenderedScheduleHtmlDocument = ViewModel.ScheduleHtmlDocument;
            }

            await Task.Yield();
            ScheduleBrowser.StartBringIntoView();
            ScheduleBrowser.Focus(FocusState.Programmatic);
        }
        catch
        {
            ScheduleBrowser.Visibility = Visibility.Collapsed;
            ScheduleEditor.Visibility = Visibility.Visible;
            ScheduleEditor.UpdateLayout();
            await Task.Yield();
            ScheduleEditor.StartBringIntoView();
            ScheduleEditor.Focus(FocusState.Programmatic);
            ScheduleEditor.Select(0, 0);
        }
    }

    private void OnScheduleButtonKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space)
        {
            return;
        }

        e.Handled = true;
        OnOpenScheduleClick(sender, new RoutedEventArgs());
    }
}
