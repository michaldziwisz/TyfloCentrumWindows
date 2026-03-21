using Microsoft.UI.Xaml.Controls;
using Tyflocentrum.Windows.UI.ViewModels;

namespace Tyflocentrum.Windows.App.Views;

public sealed partial class CommentDetailView : UserControl
{
    public CommentDetailView(CommentDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
    }

    public CommentDetailViewModel ViewModel { get; }
}
