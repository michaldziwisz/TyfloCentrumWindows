using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Playback;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class AudioPlaybackRequestFactoryTests
{
    [Fact]
    public void CreatePodcast_builds_expected_download_url_and_flags()
    {
        var factory = new AudioPlaybackRequestFactory(
            new TyfloCentrumEndpointsOptions
            {
                TyflopodcastDownloadUrl = new Uri("https://podcasts.example/pobierz.php"),
                TyfloradioStreamUrl = new Uri("https://radio.example/live.m3u8"),
            }
        );

        var request = factory.CreatePodcast(42, "Testowy podcast", "19.03.2026");

        Assert.Equal("Podcast", request.SourceTypeLabel);
        Assert.Equal("Testowy podcast", request.Title);
        Assert.Equal("19.03.2026", request.Subtitle);
        Assert.Equal("https://podcasts.example/pobierz.php?id=42&plik=0", request.SourceUrl.ToString());
        Assert.False(request.IsLive);
        Assert.True(request.CanSeek);
        Assert.True(request.CanChangePlaybackRate);
        Assert.Equal(42, request.PodcastPostId);
        Assert.Null(request.InitialSeekSeconds);
    }

    [Fact]
    public void CreateRadio_uses_configured_stream_url()
    {
        var factory = new AudioPlaybackRequestFactory(
            new TyfloCentrumEndpointsOptions
            {
                TyfloradioStreamUrl = new Uri("https://radio.example/live.m3u8"),
            }
        );

        var request = factory.CreateRadio();

        Assert.Equal("Tyfloradio", request.Title);
        Assert.Equal("https://radio.example/live.m3u8", request.SourceUrl.ToString());
        Assert.True(request.IsLive);
        Assert.False(request.CanSeek);
        Assert.False(request.CanChangePlaybackRate);
    }
}
