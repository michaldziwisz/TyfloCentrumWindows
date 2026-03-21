using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using Tyflocentrum.Windows.App.Services;
using Tyflocentrum.Windows.UI.ViewModels;
using Windows.System;

namespace Tyflocentrum.Windows.App.Views;

public sealed partial class RadioSectionView : UserControl
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly ContactTextMessageDialogService _contactTextMessageDialogService;
    private readonly ContactVoiceMessageDialogService _contactVoiceMessageDialogService;

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
        ContactButton.Focus(FocusState.Programmatic);
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
        if (!ViewModel.TryStartContact())
        {
            return;
        }

        var didSend = await _contactTextMessageDialogService.ShowAsync(XamlRoot);
        if (didSend)
        {
            ViewModel.ReportContactSent();
        }
    }

    private async void OnOpenVoiceContactClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryStartContact())
        {
            return;
        }

        var didSend = await _contactVoiceMessageDialogService.ShowAsync(XamlRoot);
        if (didSend)
        {
            ViewModel.ReportVoiceMessageSent();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
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

        AnnouncementTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.StatusAnnouncement)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ScheduleTextBlock.Visibility = ViewModel.HasScheduleText ? Visibility.Visible : Visibility.Collapsed;
        ScheduleFallbackText.Visibility = !ViewModel.HasScheduleText && ViewModel.HasLoaded
            ? Visibility.Visible
            : Visibility.Collapsed;

        RefreshButton.IsEnabled = !ViewModel.IsLoading;
        ListenButton.IsEnabled = !ViewModel.IsLoading;
        ContactButton.IsEnabled = ViewModel.CanOpenContact;
        VoiceContactButton.IsEnabled = ViewModel.CanOpenContact;
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
}
