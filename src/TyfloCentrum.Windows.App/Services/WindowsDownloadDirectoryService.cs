using TyfloCentrum.Windows.Domain.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsDownloadDirectoryService : IDownloadDirectoryService
{
    private readonly WindowHandleProvider _windowHandleProvider;

    public WindowsDownloadDirectoryService(WindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider = windowHandleProvider;
    }

    public string GetDefaultDownloadDirectoryPath()
    {
        try
        {
            var userDataPaths = UserDataPaths.GetDefault();
            if (!string.IsNullOrWhiteSpace(userDataPaths.Downloads))
            {
                return userDataPaths.Downloads;
            }
        }
        catch
        {
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "Downloads");
    }

    public string GetEffectiveDownloadDirectoryPath(string? configuredPath)
    {
        return string.IsNullOrWhiteSpace(configuredPath)
            ? GetDefaultDownloadDirectoryPath()
            : configuredPath.Trim();
    }

    public async Task<string?> PickDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (_windowHandleProvider.Handle == IntPtr.Zero)
        {
            return null;
        }

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _windowHandleProvider.Handle);

        var folder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return folder?.Path;
    }
}
