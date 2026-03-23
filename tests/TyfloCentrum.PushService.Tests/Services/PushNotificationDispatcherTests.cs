using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Options;
using TyfloCentrum.PushService.Services;
using TyfloCentrum.PushService.Tests.Support;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace TyfloCentrum.PushService.Tests.Services;

public sealed class PushNotificationDispatcherTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DispatchAsync_delivers_to_matching_windows_subscriber_and_removes_invalid_channel()
    {
        Directory.CreateDirectory(_tempDirectory);
        var stateStore = CreateStateStore();
        await stateStore.UpdateAsync(
            state =>
            {
                state.Tokens["https://example.test/channel-1"] = new PushSubscriberRecord
                {
                    Token = "https://example.test/channel-1",
                    Env = "windows-wns",
                    Prefs = new PushNotificationPreferences(true, false, false, false),
                    CreatedAt = "2026-03-21T00:00:00Z",
                    UpdatedAt = "2026-03-21T00:00:00Z",
                    LastSeenAt = "2026-03-21T00:00:00Z",
                };
                state.Tokens["https://example.test/channel-2"] = new PushSubscriberRecord
                {
                    Token = "https://example.test/channel-2",
                    Env = "windows-wns",
                    Prefs = new PushNotificationPreferences(true, false, false, false),
                    CreatedAt = "2026-03-21T00:00:00Z",
                    UpdatedAt = "2026-03-21T00:00:00Z",
                    LastSeenAt = "2026-03-21T00:00:00Z",
                };
            }
        );

        var sender = new FakeWnsNotificationSender();
        sender.Results["https://example.test/channel-2"] = PushSendResult.InvalidChannel(410);

        var dispatcher = new PushNotificationDispatcher(
            stateStore,
            sender,
            NullLogger<PushNotificationDispatcher>.Instance
        );

        var summary = await dispatcher.DispatchAsync(
            PushCategories.Podcast,
            new PushDispatchPayload(PushCategories.Podcast, "Nowy podcast", "Treść")
        );

        var state = await stateStore.ReadAsync();

        Assert.Equal(2, summary.MatchedSubscribers);
        Assert.Equal(1, summary.DeliveredNotifications);
        Assert.Equal(1, summary.RemovedSubscribers);
        Assert.Equal(2, sender.Calls.Count);
        Assert.DoesNotContain("https://example.test/channel-2", state.Tokens.Keys);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private PushStateStore CreateStateStore()
    {
        var environment = new TestHostEnvironment(_tempDirectory);
        var options = Microsoft.Extensions.Options.Options.Create(
            new PushServiceOptions { DataDirectory = _tempDirectory }
        );
        return new PushStateStore(environment, options, NullLogger<PushStateStore>.Instance);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
