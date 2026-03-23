namespace TyfloCentrum.PushService.Models;

public sealed record DispatchSummary(int MatchedSubscribers, int DeliveredNotifications, int RemovedSubscribers);
