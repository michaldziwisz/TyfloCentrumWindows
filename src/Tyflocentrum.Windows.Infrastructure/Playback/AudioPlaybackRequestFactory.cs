using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.Infrastructure.Http;

namespace Tyflocentrum.Windows.Infrastructure.Playback;

public sealed class AudioPlaybackRequestFactory : IAudioPlaybackRequestFactory
{
    private readonly TyflocentrumEndpointsOptions _options;

    public AudioPlaybackRequestFactory(TyflocentrumEndpointsOptions options)
    {
        _options = options;
    }

    public AudioPlaybackRequest CreatePodcast(
        int postId,
        string title,
        string? subtitle = null,
        double? initialSeekSeconds = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postId);

        var builder = new UriBuilder(_options.TyflopodcastDownloadUrl)
        {
            Query = $"id={postId}&plik=0",
        };

        return new AudioPlaybackRequest(
            SourceTypeLabel: "Podcast",
            Title: NormalizeTitle(title, "Podcast"),
            Subtitle: NormalizeText(subtitle),
            SourceUrl: builder.Uri,
            IsLive: false,
            CanSeek: true,
            CanChangePlaybackRate: true,
            PodcastPostId: postId,
            InitialSeekSeconds: initialSeekSeconds
        );
    }

    public AudioPlaybackRequest CreateRadio(string? subtitle = null)
    {
        return new AudioPlaybackRequest(
            SourceTypeLabel: "Tyfloradio",
            Title: "Tyfloradio",
            Subtitle: NormalizeText(subtitle) ?? "Transmisja na żywo",
            SourceUrl: _options.TyfloradioStreamUrl,
            IsLive: true,
            CanSeek: false,
            CanChangePlaybackRate: false
        );
    }

    private static string NormalizeTitle(string? value, string fallback)
    {
        var normalized = NormalizeText(value);
        return normalized ?? fallback;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
