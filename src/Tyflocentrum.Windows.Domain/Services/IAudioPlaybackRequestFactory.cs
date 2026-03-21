using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IAudioPlaybackRequestFactory
{
    AudioPlaybackRequest CreatePodcast(
        int postId,
        string title,
        string? subtitle = null,
        double? initialSeekSeconds = null
    );

    AudioPlaybackRequest CreateRadio(string? subtitle = null);
}
