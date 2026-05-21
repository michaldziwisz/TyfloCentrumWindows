using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.Web.WebView2.Core;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class RadioSectionView : UserControl
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly ContactTextMessageDialogService _contactTextMessageDialogService;
    private readonly ContactVoiceMessageDialogService _contactVoiceMessageDialogService;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private string? _lastAnnouncedStatusMessage;
    private string? _lastRenderedScheduleHtmlDocument;
    private bool _scheduleBrowserMessageHandlerAttached;
    private bool _isPrimaryScheduleMode;

    public event EventHandler? ExitToSectionListRequested;

    public RadioSectionView(
        RadioViewModel viewModel,
        AudioPlayerDialogService audioPlayerDialogService,
        ContactTextMessageDialogService contactTextMessageDialogService,
        ContactVoiceMessageDialogService contactVoiceMessageDialogService,
        IExternalLinkLauncher externalLinkLauncher
    )
    {
        _audioPlayerDialogService = audioPlayerDialogService;
        _contactTextMessageDialogService = contactTextMessageDialogService;
        _contactVoiceMessageDialogService = contactVoiceMessageDialogService;
        _externalLinkLauncher = externalLinkLauncher;
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisualState();
    }

    public RadioViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        SetPrimaryScheduleMode(false);
        ListenButton.Focus(FocusState.Programmatic);
    }

    public void SetPrimaryScheduleMode(bool isPrimaryScheduleMode)
    {
        _isPrimaryScheduleMode = isPrimaryScheduleMode;
        UpdateVisualState();
    }

    public async Task FocusScheduleContentAsync()
    {
        SetPrimaryScheduleMode(true);
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
            AnnounceStatusMessageIfNeeded(ViewModel.StatusAnnouncement);
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

        AnnouncementTextBlock.Visibility = ShouldExposeStatusAnnouncement(ViewModel.StatusAnnouncement)
            ? Visibility.Visible
            : Visibility.Collapsed;

        RefreshButton.IsEnabled = !ViewModel.IsLoading;
        ListenButton.IsEnabled = !ViewModel.IsLoading;
        ContactButton.IsEnabled = ViewModel.CanOpenContact;
        VoiceContactButton.IsEnabled = ViewModel.CanOpenContact;
        ScheduleButton.IsEnabled = ViewModel.CanOpenSchedule;
    }

    private void AnnounceStatusMessageIfNeeded(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _lastAnnouncedStatusMessage = null;
            return;
        }

        if (!ShouldExposeStatusAnnouncement(message))
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

    private bool ShouldExposeStatusAnnouncement(string? message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && (!_isPrimaryScheduleMode || IsScheduleRelevantAnnouncement(message));
    }

    private bool IsScheduleRelevantAnnouncement(string message)
    {
        return ViewModel.HasError
            && !string.Equals(
                message,
                "Tyfloradio nie prowadzi teraz audycji interaktywnej.",
                StringComparison.Ordinal
            );
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || !FocusNavigationHelper.IsFocusWithin(this))
        {
            return;
        }

        e.Handled = true;
        if (IsScheduleVisible())
        {
            CloseSchedule();
            return;
        }

        ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnOpenScheduleClick(object sender, RoutedEventArgs e)
    {
        SetPrimaryScheduleMode(false);
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
                if (!_scheduleBrowserMessageHandlerAttached)
                {
                    ScheduleBrowser.CoreWebView2.WebMessageReceived += OnScheduleBrowserWebMessageReceived;
                    ScheduleBrowser.CoreWebView2.NewWindowRequested += OnScheduleBrowserNewWindowRequested;
                    _scheduleBrowserMessageHandlerAttached = true;
                }
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

    private async void OnScheduleBrowserNavigationStarting(
        WebView2 sender,
        CoreWebView2NavigationStartingEventArgs args
    )
    {
        if (!TryGetExternalScheduleLink(args.Uri, out var target))
        {
            return;
        }

        args.Cancel = true;
        await OpenScheduleLinkAsync(target);
    }

    private async void OnScheduleBrowserNewWindowRequested(
        CoreWebView2 sender,
        CoreWebView2NewWindowRequestedEventArgs args
    )
    {
        if (!TryGetExternalScheduleLink(args.Uri, out var target))
        {
            return;
        }

        args.Handled = true;
        await OpenScheduleLinkAsync(target);
    }

    private async void OnScheduleBrowserWebMessageReceived(
        CoreWebView2 sender,
        CoreWebView2WebMessageReceivedEventArgs args
    )
    {
        var message = args.TryGetWebMessageAsString();
        if (string.Equals(message, "closeSchedule", StringComparison.Ordinal))
        {
            CloseSchedule();
            return;
        }

        const string openExternalPrefix = "openExternal:";
        if (!message.StartsWith(openExternalPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var target = message[openExternalPrefix.Length..];
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await OpenScheduleLinkAsync(target);
    }

    private async Task OpenScheduleLinkAsync(string target)
    {
        var launched = await _externalLinkLauncher.LaunchAsync(target);
        if (!launched)
        {
            AutomationAnnouncementHelper.Announce(
                AnnouncementTextBlock,
                "Nie udało się otworzyć linku z ramówki Tyfloradia.",
                important: true
            );
        }
    }

    private void CloseSchedule()
    {
        ScheduleBrowser.Visibility = Visibility.Collapsed;
        ScheduleEditor.Visibility = Visibility.Collapsed;
        if (_isPrimaryScheduleMode)
        {
            _isPrimaryScheduleMode = false;
            ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        ScheduleButton.Focus(FocusState.Programmatic);
        AutomationAnnouncementHelper.Announce(
            AnnouncementTextBlock,
            "Zamknięto ramówkę Tyfloradia.",
            important: true
        );
    }

    private bool IsScheduleVisible()
    {
        return ScheduleBrowser.Visibility == Visibility.Visible
            || ScheduleEditor.Visibility == Visibility.Visible;
    }

    private static bool TryGetExternalScheduleLink(string? candidate, out string target)
    {
        target = string.Empty;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (
            uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase)
        )
        {
            target = uri.AbsoluteUri;
            return true;
        }

        return false;
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
