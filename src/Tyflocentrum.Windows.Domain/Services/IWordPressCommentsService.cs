using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IWordPressCommentsService
{
    Task<IReadOnlyList<WordPressComment>> GetCommentsAsync(
        int postId,
        CancellationToken cancellationToken = default
    );
}
