namespace TyfloCentrum.Windows.Domain.Services;

public interface IDownloadDirectoryService
{
    string GetDefaultDownloadDirectoryPath();

    string GetEffectiveDownloadDirectoryPath(string? configuredPath);
}
