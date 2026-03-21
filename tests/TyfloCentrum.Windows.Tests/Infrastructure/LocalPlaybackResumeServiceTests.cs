using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class LocalPlaybackResumeServiceTests
{
    [Fact]
    public async Task SaveResumePositionAsync_and_GetResumePositionAsync_roundtrip_valid_value()
    {
        var service = new LocalPlaybackResumeService(new InMemoryLocalSettingsStore());
        var sourceUrl = new Uri("https://podcasts.example/pobierz.php?id=42&plik=0");

        await service.SaveResumePositionAsync(sourceUrl, 123.456);
        var actual = await service.GetResumePositionAsync(sourceUrl);

        Assert.Equal(123.456, actual);
    }

    [Fact]
    public async Task GetResumePositionAsync_returns_null_for_values_at_or_below_threshold()
    {
        var service = new LocalPlaybackResumeService(new InMemoryLocalSettingsStore());
        var sourceUrl = new Uri("https://podcasts.example/pobierz.php?id=7&plik=0");

        await service.SaveResumePositionAsync(sourceUrl, 1);
        var actual = await service.GetResumePositionAsync(sourceUrl);

        Assert.Null(actual);
    }

    [Fact]
    public async Task ClearResumePositionAsync_removes_previously_saved_value()
    {
        var service = new LocalPlaybackResumeService(new InMemoryLocalSettingsStore());
        var sourceUrl = new Uri("https://podcasts.example/pobierz.php?id=99&plik=0");

        await service.SaveResumePositionAsync(sourceUrl, 88);
        await service.ClearResumePositionAsync(sourceUrl);
        var actual = await service.GetResumePositionAsync(sourceUrl);

        Assert.Null(actual);
    }
}
