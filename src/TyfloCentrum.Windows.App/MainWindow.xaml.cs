using Microsoft.UI.Xaml;
using TyfloCentrum.Windows.App.Views;

namespace TyfloCentrum.Windows.App;

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
