using System.Runtime.InteropServices;
using TyfloCentrum.Windows.Domain.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using WinRT;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsShareService : IShareService
{
    private DataTransferManager? _dataTransferManager;
    private TypedEventHandler<DataTransferManager, DataRequestedEventArgs>? _dataRequestedHandler;
    private readonly WindowHandleProvider _windowHandleProvider;

    public WindowsShareService(WindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider = windowHandleProvider;
    }

    public Task<bool> ShareLinkAsync(
        string title,
        string? description,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (
            string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(url)
            || _windowHandleProvider.Handle == IntPtr.Zero
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
        )
        {
            return Task.FromResult(false);
        }

        try
        {
            var dataTransferManager = DataTransferManagerHelper.GetForWindow(
                _windowHandleProvider.Handle
            );

            if (_dataTransferManager is not null && _dataRequestedHandler is not null)
            {
                _dataTransferManager.DataRequested -= _dataRequestedHandler;
            }

            _dataRequestedHandler = (_, args) =>
            {
                var request = args.Request;
                request.Data.Properties.Title = title;
                request.Data.Properties.Description = string.IsNullOrWhiteSpace(description)
                    ? title
                    : description;
                request.Data.SetWebLink(uri);
                request.Data.SetText(uri.AbsoluteUri);
                request.Data.RequestedOperation = DataPackageOperation.Copy;
            };

            dataTransferManager.DataRequested += _dataRequestedHandler;
            _dataTransferManager = dataTransferManager;
            DataTransferManagerHelper.ShowShareUIForWindow(_windowHandleProvider.Handle);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static class DataTransferManagerHelper
    {
        public static DataTransferManager GetForWindow(nint windowHandle)
        {
            var interop = DataTransferManager.As<IDataTransferManagerInterop>();
            var dataTransferManager = interop.GetForWindow(
                windowHandle,
                DataTransferManagerInteropGuid
            );
            return MarshalInterface<DataTransferManager>.FromAbi(dataTransferManager);
        }

        public static void ShowShareUIForWindow(nint windowHandle)
        {
            var interop = DataTransferManager.As<IDataTransferManagerInterop>();
            interop.ShowShareUIForWindow(windowHandle);
        }

        private static readonly Guid DataTransferManagerInteropGuid = new(
            0xa5caee9b,
            0x8708,
            0x49d1,
            0x8d,
            0x36,
            0x67,
            0xd2,
            0x5a,
            0x8d,
            0xa0,
            0x0c
        );

        [ComImport]
        [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDataTransferManagerInterop
        {
            IntPtr GetForWindow(nint appWindow, in Guid riid);

            void ShowShareUIForWindow(nint appWindow);
        }
    }
}
