using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;
using System.Text;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class InAppBrowserView : UserControl
{
    private const string ReaderAccessibleName = "Czytnik artykułu";
    private static readonly Guid AccPropServicesClsid = new("B5F8350B-0548-48B1-A6EE-88BD00B4A5E7");
    private static readonly Guid NamePropertyId = new("608D3DF8-8128-4AA7-A428-F55E49267291");
    private static readonly IAccPropServicesNative AccPropServices =
        (IAccPropServicesNative)Activator.CreateInstance(Type.GetTypeFromCLSID(AccPropServicesClsid)!)!;
    private const uint ObjIdWindow = 0;
    private const uint ObjIdClient = 0xFFFFFFFC;
    private const uint ChildIdSelf = 0;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly Services.WindowHandleProvider _windowHandleProvider;
    private readonly HashSet<nint> _annotatedBrowserClientHandles = [];
    private string _currentHtml = string.Empty;
    private bool _isInitialized;
    private bool _messageHandlerAttached;
    private Uri? _pendingUri;

    public InAppBrowserView(
        IExternalLinkLauncher externalLinkLauncher,
        Services.WindowHandleProvider windowHandleProvider
    )
    {
        _externalLinkLauncher = externalLinkLauncher;
        _windowHandleProvider = windowHandleProvider;
        InitializeComponent();
    }

    public Action? CloseRequested { get; set; }

    public bool Initialize(string title, string link, string readerHtml)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(readerHtml))
        {
            return false;
        }

        _pendingUri = uri;
        _currentHtml = readerHtml;
        _isInitialized = false;
        ViewTitleTextBlock.Text = string.IsNullOrWhiteSpace(title) ? "Artykuł" : title.Trim();
        ErrorBar.IsOpen = false;
        ErrorBar.Message = string.Empty;
        LoadingIndicator.IsActive = false;
        LoadingIndicator.Visibility = Visibility.Collapsed;
        return true;
    }

    public void Cleanup()
    {
        try
        {
            ClearBrowserAccessibilityAnnotation();

            if (BrowserView.CoreWebView2 is not null && _messageHandlerAttached)
            {
                BrowserView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _messageHandlerAttached = false;
            }

            BrowserView.CoreWebView2?.Stop();
            BrowserView.Source = null;
        }
        catch
        {
            // Ignore cleanup errors when the dialog is already closing.
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_pendingUri is null || _isInitialized)
        {
            return;
        }

        try
        {
            SetLoadingState(true);
            await BrowserView.EnsureCoreWebView2Async();

            if (BrowserView.CoreWebView2 is not null && !_messageHandlerAttached)
            {
                BrowserView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                BrowserView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                BrowserView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _messageHandlerAttached = true;
            }

            BrowserView.NavigateToString(_currentHtml);
            _isInitialized = true;
        }
        catch
        {
            ShowError("Nie udało się otworzyć artykułu w aplikacji.");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Cleanup();
    }

    private void OnNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        ErrorBar.IsOpen = false;
        ErrorBar.Message = string.Empty;
        SetLoadingState(true);
    }

    private async void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        SetLoadingState(false);

        if (!args.IsSuccess)
        {
            ShowError("Nie udało się załadować artykułu.");
            return;
        }

        await FocusReaderAsync();
        _ = AnnotateBrowserAccessibilityClientsAsync();
    }

    private void OnBrowserViewGotFocus(object sender, RoutedEventArgs e)
    {
        _ = AnnotateBrowserAccessibilityClientsAsync();
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingIndicator.IsActive = isLoading;
        LoadingIndicator.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        var message = args.TryGetWebMessageAsString();

        if (string.Equals(message, "close", StringComparison.Ordinal))
        {
            CloseRequested?.Invoke();
            return;
        }

        const string prefix = "openExternal:";
        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var link = message[prefix.Length..];
        if (string.IsNullOrWhiteSpace(link))
        {
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(link);
        if (!launched)
        {
            ShowError("Nie udało się otworzyć artykułu w zewnętrznej przeglądarce.");
        }
    }

    private async Task FocusReaderAsync()
    {
        BrowserView.Focus(FocusState.Programmatic);

        if (BrowserView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await BrowserView.CoreWebView2.ExecuteScriptAsync(
                "const root = document.getElementById('article-root'); if (root) { root.focus(); }"
            );
        }
        catch
        {
            // Ignore focus script failures. The control already has focus.
        }
    }

    private async Task AnnotateBrowserAccessibilityClientsAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            TryAnnotateBrowserAccessibilityClients();

            if (_annotatedBrowserClientHandles.Count > 0)
            {
                return;
            }

            await Task.Delay(75);
        }
    }

    private void TryAnnotateBrowserAccessibilityClients()
    {
        var mainWindowHandle = _windowHandleProvider.Handle;
        if (mainWindowHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            EnumChildWindows(mainWindowHandle, AnnotateBrowserAccessibilityClientHandle, IntPtr.Zero);
        }
        catch
        {
            // Ignore annotation failures. The reader remains usable without them.
        }
    }

    private void ClearBrowserAccessibilityAnnotation()
    {
        if (_annotatedBrowserClientHandles.Count == 0)
        {
            return;
        }

        foreach (var annotatedHandle in _annotatedBrowserClientHandles.ToArray())
        {
            try
            {
                var handle = CreateRemotableHandle(annotatedHandle);
                var prop = NamePropertyId;
                AccPropServices.ClearHwndProps(ref handle, ObjIdClient, ChildIdSelf, ref prop, 1);
            }
            catch
            {
                // Ignore cleanup failures when the browser handle no longer exists.
            }
        }

        _annotatedBrowserClientHandles.Clear();
    }

    private int AnnotateBrowserAccessibilityClientHandle(nint handle, nint lParam)
    {
        if (_annotatedBrowserClientHandles.Contains(handle))
        {
            return 1;
        }

        var className = GetWindowClassName(handle);
        if (className.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal))
        {
            ApplyAccessibilityMetadata(handle);
            _annotatedBrowserClientHandles.Add(handle);
        }

        return 1;
    }

    private static void ApplyAccessibilityMetadata(nint handle)
    {
        var remotableHandle = CreateRemotableHandle(handle);
        AccPropServices.SetHwndPropStr(
            ref remotableHandle,
            ObjIdWindow,
            ChildIdSelf,
            NamePropertyId,
            ReaderAccessibleName
        );
        AccPropServices.SetHwndPropStr(
            ref remotableHandle,
            ObjIdClient,
            ChildIdSelf,
            NamePropertyId,
            ReaderAccessibleName
        );
        SetWindowText(handle, ReaderAccessibleName);
    }

    private static string GetWindowClassName(nint handle)
    {
        var buffer = new StringBuilder(256);
        _ = GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static RemotableHandle CreateRemotableHandle(nint handle)
    {
        return new RemotableHandle
        {
            fContext = 0,
            u = new RemotableHandleUnion
            {
                hInproc = unchecked((int)handle.ToInt64()),
            },
        };
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowText(nint hWnd, string lpString);

    private delegate int EnumWindowsProc(nint hWnd, nint lParam);

    [ComImport]
    [Guid("6E26E776-04F0-495D-80E4-3330352E3169")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAccPropServicesNative
    {
        void SetPropValue(ref byte pIDString, uint dwIDStringLen, Guid idProp, object value);
        void SetPropServer(
            ref byte pIDString,
            uint dwIDStringLen,
            ref Guid paProps,
            int cProps,
            nint pServer,
            int annoScope
        );
        void ClearProps(ref byte pIDString, uint dwIDStringLen, ref Guid paProps, int cProps);
        void SetHwndProp(
            ref RemotableHandle hwnd,
            uint idObject,
            uint idChild,
            Guid idProp,
            object value
        );
        void SetHwndPropStr(
            ref RemotableHandle hwnd,
            uint idObject,
            uint idChild,
            Guid idProp,
            [MarshalAs(UnmanagedType.BStr)] string value
        );
        void SetHwndPropServer(
            ref RemotableHandle hwnd,
            uint idObject,
            uint idChild,
            ref Guid paProps,
            int cProps,
            nint pServer,
            int annoScope
        );
        void ClearHwndProps(
            ref RemotableHandle hwnd,
            uint idObject,
            uint idChild,
            ref Guid paProps,
            int cProps
        );
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RemotableHandle
    {
        public int fContext;
        public RemotableHandleUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RemotableHandleUnion
    {
        [FieldOffset(0)]
        public int hInproc;

        [FieldOffset(0)]
        public int hRemote;
    }
}
