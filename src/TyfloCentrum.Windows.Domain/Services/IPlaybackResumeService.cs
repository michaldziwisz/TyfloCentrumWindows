namespace TyfloCentrum.Windows.Domain.Services;

public interface IPlaybackResumeService
{
    Task<double?> GetResumePositionAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken = default
    );

    Task SaveResumePositionAsync(
        Uri sourceUrl,
        double positionSeconds,
        CancellationToken cancellationToken = default
    );

    Task ClearResumePositionAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken = default
    );
}
