using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class ContactTextMessageView : UserControl
{
    public ContactTextMessageView(ContactTextMessageViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisualState();
    }

    public ContactTextMessageViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        FocusInitialField();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FocusInitialField();
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SendAsync();
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

        SendButton.Content = ViewModel.SendButtonText;
        SendButton.IsEnabled = ViewModel.CanSend;
        SendingIndicator.IsActive = ViewModel.IsSending;
        SendingIndicator.Visibility = ViewModel.IsSending ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FocusInitialField()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.Name))
        {
            NameTextBox.Focus(FocusState.Programmatic);
            return;
        }

        MessageTextBox.Focus(FocusState.Programmatic);
    }
}
