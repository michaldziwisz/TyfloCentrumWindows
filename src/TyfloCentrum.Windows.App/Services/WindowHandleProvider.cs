using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowHandleProvider
{
    public nint Handle { get; private set; }

    public void Initialize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = WindowNative.GetWindowHandle(window);
        if (handle != IntPtr.Zero)
        {
            Handle = handle;
        }
    }
}
