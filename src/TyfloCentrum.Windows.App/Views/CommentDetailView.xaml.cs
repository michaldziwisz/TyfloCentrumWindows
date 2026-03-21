using Microsoft.UI.Xaml.Controls;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.App.Views;

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
