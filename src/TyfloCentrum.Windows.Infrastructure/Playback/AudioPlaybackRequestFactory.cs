using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;

namespace TyfloCentrum.Windows.Infrastructure.Playback;

public sealed class AudioPlaybackRequestFactory : IAudioPlaybackRequestFactory
{
    private readonly TyfloCentrumEndpointsOptions _options;

    public AudioPlaybackRequestFactory(TyfloCentrumEndpointsOptions options)
    {
        _options = options;
    }

    public Uri CreatePodcastDownloadUri(int postId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postId);

        var builder = new UriBuilder(_options.TyflopodcastDownloadUrl)
        {
            Query = $"id={postId}&plik=0",
        };

        return builder.Uri;
    }

    public AudioPlaybackRequest CreatePodcast(
        int postId,
        string title,
        string? subtitle = null,
        double? initialSeekSeconds = null
    )
    {
        return new AudioPlaybackRequest(
            SourceTypeLabel: "Podcast",
            Title: NormalizeTitle(title, "Podcast"),
            Subtitle: NormalizeText(subtitle),
            SourceUrl: CreatePodcastDownloadUri(postId),
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
