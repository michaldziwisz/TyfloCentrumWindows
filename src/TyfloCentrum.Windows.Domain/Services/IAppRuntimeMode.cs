namespace TyfloCentrum.Windows.Domain.Services;

public interface IAppRuntimeMode
{
    bool HasPackageIdentity { get; }

    bool SupportsSystemNotifications { get; }

    bool SupportsPushNotifications { get; }
}
