namespace TyfloCentrum.Windows.Domain.Services;

public interface IShareService
{
    Task<bool> ShareLinkAsync(
        string title,
        string? description,
        string url,
        CancellationToken cancellationToken = default
    );
}
