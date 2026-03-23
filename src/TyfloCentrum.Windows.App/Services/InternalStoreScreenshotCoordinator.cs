using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace TyfloCentrum.Windows.App.Services;

internal sealed class InternalStoreScreenshotCoordinator
{
    private readonly ILogger<InternalStoreScreenshotCoordinator> _logger;
    private readonly WindowHandleProvider _windowHandleProvider;

    public InternalStoreScreenshotCoordinator(
        ILogger<InternalStoreScreenshotCoordinator> logger,
        WindowHandleProvider windowHandleProvider
    )
    {
        _logger = logger;
        _windowHandleProvider = windowHandleProvider;
    }

    public bool HasPendingRequest()
    {
        return TryGetPendingRequest() is not null;
    }

    public async Task<bool> TryRunAsync(global::TyfloCentrum.Windows.App.MainWindow mainWindow)
    {
        var request = TryGetPendingRequest();
        if (request is null)
        {
            return false;
        }

        try
        {
            ResizeWindow(mainWindow, width: 1440, height: 900);
            await mainWindow.CaptureInternalStoreScreenshotAsync(request);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to capture internal store screenshot.");
            throw;
        }
        finally
        {
            Application.Current.Exit();
        }

        return true;
    }

    private InternalStoreScreenshotRequest? TryGetPendingRequest()
    {
        var arguments = Environment.GetCommandLineArgs();
        return InternalStoreScreenshotRequestParser.Parse(arguments);
    }

    private void ResizeWindow(Window window, int width, int height)
    {
        var handle = _windowHandleProvider.Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            windowId,
            Microsoft.UI.Windowing.DisplayAreaFallback.Primary
        );

        var workArea = displayArea.WorkArea;
        var safeWidth = Math.Min(width, workArea.Width - 80);
        var safeHeight = Math.Min(height, workArea.Height - 80);
        var positionX = workArea.X + Math.Max(0, (workArea.Width - safeWidth) / 2);
        var positionY = workArea.Y + Math.Max(0, (workArea.Height - safeHeight) / 2);

        appWindow.Resize(new global::Windows.Graphics.SizeInt32(safeWidth, safeHeight));
        appWindow.Move(new global::Windows.Graphics.PointInt32(positionX, positionY));
    }
}
