using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IAudioPlaybackRequestFactory
{
    Uri CreatePodcastDownloadUri(int postId);

    AudioPlaybackRequest CreatePodcast(
        int postId,
        string title,
        string? subtitle = null,
        double? initialSeekSeconds = null
    );

    AudioPlaybackRequest CreateRadio(string? subtitle = null);
}
