using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TyfloCentrum.Windows.App.Views;

namespace TyfloCentrum.Windows.App;

public sealed partial class MainWindow : Window
{
    private readonly Services.WindowHandleProvider _windowHandleProvider;
    private bool _maximizeRequested;

    public ShellPage ShellPage { get; }

    public MainWindow(ShellPage shellPage, Services.WindowHandleProvider windowHandleProvider)
    {
        InitializeComponent();
        Title = "TyfloCentrum";
        ShellPage = shellPage;
        Content = shellPage;
        _windowHandleProvider = windowHandleProvider;
        windowHandleProvider.Initialize(this);
    }

    public void EnsureMaximized()
    {
        if (_maximizeRequested)
        {
            return;
        }

        _maximizeRequested = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            var handle = _windowHandleProvider.Handle;
            if (handle == IntPtr.Zero)
            {
                _maximizeRequested = false;
                return;
            }

            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow.Presenter is not OverlappedPresenter)
            {
                appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            }

            if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
            {
                overlappedPresenter.Maximize();
            }
        });
    }

    internal Task CaptureInternalStoreScreenshotAsync(
        Services.InternalStoreScreenshotRequest request
    )
    {
        return ShellPage.CaptureInternalStoreScreenshotAsync(request);
    }
}
