using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IContentDownloadService
{
    Task<string> DownloadPodcastAsync(
        int postId,
        string title,
        CancellationToken cancellationToken = default
    );

    Task<string> DownloadArticleAsync(
        ContentSource source,
        int postId,
        string title,
        string fallbackDate,
        string fallbackLink,
        CancellationToken cancellationToken = default
    );

    Task<string> DownloadTyfloSwiatPageAsync(
        int pageId,
        string title,
        string fallbackDate,
        string fallbackLink,
        CancellationToken cancellationToken = default
    );
}
