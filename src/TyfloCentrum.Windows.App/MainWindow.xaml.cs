using Microsoft.UI.Xaml;
using TyfloCentrum.Windows.App.Views;

namespace TyfloCentrum.Windows.App;

public sealed partial class MainWindow : Window
{
    public ShellPage ShellPage { get; }

    public MainWindow(ShellPage shellPage, Services.WindowHandleProvider windowHandleProvider)
    {
        InitializeComponent();
        Title = "TyfloCentrum";
        ShellPage = shellPage;
        Content = shellPage;
        windowHandleProvider.Initialize(this);
    }

    internal Task CaptureInternalStoreScreenshotAsync(
        Services.InternalStoreScreenshotRequest request
    )
    {
        return ShellPage.CaptureInternalStoreScreenshotAsync(request);
    }
}
