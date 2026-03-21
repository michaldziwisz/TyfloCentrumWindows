using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IWordPressPostDetailsService
{
    Task<WpPostDetail> GetPostAsync(
        ContentSource source,
        int postId,
        CancellationToken cancellationToken = default
    );
}
