using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IWordPressPageDetailsService
{
    Task<WpPostDetail> GetPageAsync(
        ContentSource source,
        int pageId,
        CancellationToken cancellationToken = default
    );

    Task<WpPostDetail?> GetPageBySlugAsync(
        ContentSource source,
        string slug,
        CancellationToken cancellationToken = default
    );
}
