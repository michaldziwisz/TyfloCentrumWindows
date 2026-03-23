using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Services;

namespace TyfloCentrum.PushService.Tests.Support;

internal sealed class FakeWnsNotificationSender : IWnsNotificationSender
{
    public List<(string ChannelUri, PushDispatchPayload Payload)> Calls { get; } = [];

    public Dictionary<string, PushSendResult> Results { get; } = new(StringComparer.Ordinal);

    public Task<PushSendResult> SendToastAsync(
        string channelUri,
        PushDispatchPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        Calls.Add((channelUri, payload));
        return Task.FromResult(
            Results.TryGetValue(channelUri, out var result)
                ? result
                : PushSendResult.Delivered()
        );
    }
}
