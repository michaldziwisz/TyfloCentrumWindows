using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using Tyflocentrum.Windows.App.Services;
using Tyflocentrum.Windows.UI.ViewModels;
using Windows.System;

namespace Tyflocentrum.Windows.App.Views;

public sealed partial class SettingsSectionView : UserControl
{
    public event EventHandler? ExitToSectionListRequested;

    public SettingsSectionView(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisualState();
    }

    public SettingsViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        SaveButton.Focus(FocusState.Programmatic);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadIfNeededAsync();
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        return ViewModel.RefreshAsync(cancellationToken);
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
    }

    private async void OnRefreshDevicesClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ResetAudioSettingsAsync();
    }

    private async void OnClearRememberedRateClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearRememberedPlaybackRateAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        LoadingIndicator.IsActive = ViewModel.IsLoading || ViewModel.IsSaving;
        LoadingIndicator.Visibility = ViewModel.IsLoading || ViewModel.IsSaving
            ? Visibility.Visible
            : Visibility.Collapsed;

        ErrorPanel.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.IsOpen = ViewModel.HasError;
        ErrorBar.Message = ViewModel.ErrorMessage;

        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.StatusMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

        SaveButton.IsEnabled = ViewModel.CanSave;
        RefreshDevicesButton.IsEnabled = ViewModel.CanRefreshDevices;
        ResetButton.IsEnabled = ViewModel.CanResetAudioSettings;
        ClearRememberedRateButton.IsEnabled = ViewModel.CanClearRememberedPlaybackRate;
        InputDeviceComboBox.IsEnabled = !ViewModel.IsLoading && !ViewModel.IsSaving;
        OutputDeviceComboBox.IsEnabled = !ViewModel.IsLoading && !ViewModel.IsSaving;
        DefaultPlaybackRateComboBox.IsEnabled = !ViewModel.IsLoading && !ViewModel.IsSaving;
        RememberLastRateToggle.IsEnabled = !ViewModel.IsLoading && !ViewModel.IsSaving;
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
