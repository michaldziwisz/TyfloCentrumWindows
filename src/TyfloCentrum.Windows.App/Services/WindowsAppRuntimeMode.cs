using System.Runtime.InteropServices;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsAppRuntimeMode : IAppRuntimeMode
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    public WindowsAppRuntimeMode()
    {
        HasPackageIdentity = DetectPackageIdentity();
    }

    public bool HasPackageIdentity { get; }

    public bool SupportsSystemNotifications => HasPackageIdentity;

    public bool SupportsPushNotifications => HasPackageIdentity;

    private static bool DetectPackageIdentity()
    {
        uint packageFullNameLength = 0;
        var result = GetCurrentPackageFullName(ref packageFullNameLength, null);
        return result != APPMODEL_ERROR_NO_PACKAGE;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, string? packageFullName);
}
