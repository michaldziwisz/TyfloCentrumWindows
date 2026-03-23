using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class LocalContentNotificationStateStoreTests
{
    [Fact]
    public async Task GetAsync_returns_empty_state_when_store_is_empty()
    {
        var service = new LocalContentNotificationStateStore(new InMemoryLocalSettingsStore());

        var state = await service.GetAsync();

        Assert.Equal(ContentNotificationStateSnapshot.Empty, state);
    }

    [Fact]
    public async Task SaveAsync_persists_roundtrip_values()
    {
        var service = new LocalContentNotificationStateStore(new InMemoryLocalSettingsStore());
        var expected = new ContentNotificationStateSnapshot(123, 456);

        await service.SaveAsync(expected);
        var actual = await service.GetAsync();

        Assert.Equal(expected, actual);
    }
}
