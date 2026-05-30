using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IPodcastTextVersionService
{
    Task<PodcastTextVersionDocument?> GetAsync(
        RelatedLink textVersionLink,
        string fallbackTitle,
        string fallbackDate,
        CancellationToken cancellationToken = default
    );
}
