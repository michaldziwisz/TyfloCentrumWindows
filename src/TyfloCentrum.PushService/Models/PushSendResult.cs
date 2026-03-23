namespace TyfloCentrum.PushService.Models;

public sealed record PushSendResult(PushSendStatus Status, int? StatusCode = null)
{
    public static PushSendResult Delivered() => new(PushSendStatus.Delivered);

    public static PushSendResult InvalidChannel(int statusCode) =>
        new(PushSendStatus.InvalidChannel, statusCode);

    public static PushSendResult Failed(int? statusCode = null) =>
        new(PushSendStatus.Failed, statusCode);
}
