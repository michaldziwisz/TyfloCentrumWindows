using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IPodcastShowNotesService
{
    Task<PodcastShowNotesSnapshot> GetAsync(
        int postId,
        CancellationToken cancellationToken = default
    );
}
