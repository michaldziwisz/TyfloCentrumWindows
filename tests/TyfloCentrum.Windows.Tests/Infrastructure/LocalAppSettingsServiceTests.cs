using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class LocalAppSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_returns_defaults_when_store_is_empty()
    {
        var service = new LocalAppSettingsService(new InMemoryLocalSettingsStore());

        var settings = await service.GetAsync();

        Assert.Equal(AppSettingsSnapshot.Defaults, settings);
        Assert.Equal(PlaybackRateCatalog.DefaultValue, settings.EffectivePlaybackRate);
    }

    [Fact]
    public async Task SaveAsync_persists_roundtrip_values()
    {
        var service = new LocalAppSettingsService(new InMemoryLocalSettingsStore());
        var expected = new AppSettingsSnapshot(
            "mic-1",
            "speaker-1",
            @"D:\Pobrane\TyfloCentrum",
            1.25,
            true,
            1.5,
            false,
            true,
            true,
            42d,
            ContentTypeAnnouncementPlacement.AfterTitle
        );

        await service.SaveAsync(expected);
        var actual = await service.GetAsync();

        Assert.Equal("mic-1", actual.PreferredInputDeviceId);
        Assert.Equal("speaker-1", actual.PreferredOutputDeviceId);
        Assert.Equal(@"D:\Pobrane\TyfloCentrum", actual.DownloadDirectoryPath);
        Assert.Equal(1.25, actual.DefaultPlaybackRate);
        Assert.True(actual.RememberLastPlaybackRate);
        Assert.Equal(1.5, actual.LastPlaybackRate);
        Assert.Equal(1.5, actual.EffectivePlaybackRate);
        Assert.False(actual.NotifyAboutNewPodcasts);
        Assert.True(actual.NotifyAboutNewArticles);
        Assert.True(actual.RememberLastPlaybackVolume);
        Assert.Equal(42d, actual.LastPlaybackVolumePercent);
        Assert.Equal(42d, actual.EffectivePlaybackVolumePercent);
        Assert.Equal(
            ContentTypeAnnouncementPlacement.AfterTitle,
            actual.ContentTypeAnnouncementPlacement
        );
    }
}
