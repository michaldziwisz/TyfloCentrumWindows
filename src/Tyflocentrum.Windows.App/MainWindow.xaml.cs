using Microsoft.UI.Xaml;
using Tyflocentrum.Windows.App.Views;

namespace Tyflocentrum.Windows.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(ShellPage shellPage, Services.WindowHandleProvider windowHandleProvider)
    {
        InitializeComponent();
        Title = "TyfloCentrum";
        Content = shellPage;
        windowHandleProvider.Initialize(this);
    }
}
