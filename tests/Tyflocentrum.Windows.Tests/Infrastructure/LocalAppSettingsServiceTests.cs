using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace Tyflocentrum.Windows.Tests.Infrastructure;

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
            1.25,
            true,
            1.5
        );

        await service.SaveAsync(expected);
        var actual = await service.GetAsync();

        Assert.Equal("mic-1", actual.PreferredInputDeviceId);
        Assert.Equal("speaker-1", actual.PreferredOutputDeviceId);
        Assert.Equal(1.25, actual.DefaultPlaybackRate);
        Assert.True(actual.RememberLastPlaybackRate);
        Assert.Equal(1.5, actual.LastPlaybackRate);
        Assert.Equal(1.5, actual.EffectivePlaybackRate);
    }
}
