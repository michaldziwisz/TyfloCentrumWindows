using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class FeedbackSectionView : UserControl
{
    public event EventHandler? ExitToSectionListRequested;

    public FeedbackSectionView(FeedbackSectionViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisualState();
    }

    public FeedbackSectionViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        KindComboBox.Focus(FocusState.Programmatic);
    }

    private async void OnSubmitClick(object sender, RoutedEventArgs e)
    {
        await Task.Yield();
        await ViewModel.SubmitAsync();
        if (ViewModel.HasPublicIssueUrl)
        {
            OpenIssueButton.Focus(FocusState.Programmatic);
        }
        else if (ViewModel.HasError)
        {
            TitleTextBox.Focus(FocusState.Programmatic);
        }
    }

    private async void OnOpenIssueClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.OpenPublicIssueAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        StatusTextBlock.Text = ViewModel.StatusMessage ?? string.Empty;
        StatusTextBlock.Visibility = ViewModel.HasStatus ? Visibility.Visible : Visibility.Collapsed;

        ErrorBar.IsOpen = ViewModel.HasError;
        ErrorBar.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.Message = ViewModel.ErrorMessage;

        SubmitButton.IsEnabled = ViewModel.CanSubmit;
        SubmitButton.Content = ViewModel.SubmitButtonText;
        OpenIssueButton.IsEnabled = ViewModel.CanOpenPublicIssue;
        OpenIssueButton.Visibility = ViewModel.HasPublicIssueUrl
            ? Visibility.Visible
            : Visibility.Collapsed;
        SubmittingIndicator.IsActive = ViewModel.IsSubmitting;
        SubmittingIndicator.Visibility = ViewModel.IsSubmitting
            ? Visibility.Visible
            : Visibility.Collapsed;
        KindComboBox.IsEnabled = !ViewModel.IsSubmitting;
        TitleTextBox.IsEnabled = !ViewModel.IsSubmitting;
        DescriptionTextBox.IsEnabled = !ViewModel.IsSubmitting;
        ContactEmailTextBox.IsEnabled = !ViewModel.IsSubmitting;
        AllowPrivateContactByEmailCheckBox.IsEnabled = !ViewModel.IsSubmitting;
        IncludeDiagnosticsCheckBox.IsEnabled = !ViewModel.IsSubmitting;
        IncludeLogCheckBox.IsEnabled = !ViewModel.IsSubmitting;
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
