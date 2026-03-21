using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class TyfloSwiatPageDetailView : UserControl
{
    public TyfloSwiatPageDetailView(TyfloSwiatPageDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisualState();
    }

    public TyfloSwiatPageDetailViewModel ViewModel { get; }

    public Func<Task>? ShareRequestHandler { get; set; }

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

        ContentTextBlock.Visibility = ViewModel.HasContent ? Visibility.Visible : Visibility.Collapsed;
    }
}
