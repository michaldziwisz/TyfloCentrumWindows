using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IWordPressPostDetailsService
{
    Task<WpPostDetail> GetPostAsync(
        ContentSource source,
        int postId,
        CancellationToken cancellationToken = default
    );
}
