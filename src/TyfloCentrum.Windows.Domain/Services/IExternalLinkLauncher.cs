namespace TyfloCentrum.Windows.Domain.Services;

public interface IExternalLinkLauncher
{
    Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default);
}
