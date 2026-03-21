using TyfloCentrum.Windows.Domain.Services;
using Windows.System;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsExternalLinkLauncher : IExternalLinkLauncher
{
    public async Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return await Launcher.LaunchUriAsync(uri);
    }
}
