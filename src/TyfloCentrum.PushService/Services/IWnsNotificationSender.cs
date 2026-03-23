using TyfloCentrum.PushService.Models;

namespace TyfloCentrum.PushService.Services;

public interface IWnsNotificationSender
{
    Task<PushSendResult> SendToastAsync(
        string channelUri,
        PushDispatchPayload payload,
        CancellationToken cancellationToken = default
    );
}
