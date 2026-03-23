using TyfloCentrum.PushService.Models;

namespace TyfloCentrum.PushService.Services;

public sealed class PushNotificationDispatcher
{
    private readonly PushStateStore _stateStore;
    private readonly IWnsNotificationSender _wnsNotificationSender;
    private readonly ILogger<PushNotificationDispatcher> _logger;

    public PushNotificationDispatcher(
        PushStateStore stateStore,
        IWnsNotificationSender wnsNotificationSender,
        ILogger<PushNotificationDispatcher> logger
    )
    {
        _stateStore = stateStore;
        _wnsNotificationSender = wnsNotificationSender;
        _logger = logger;
    }

    public async Task<DispatchSummary> DispatchAsync(
        string category,
        PushDispatchPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await _stateStore.ReadAsync(cancellationToken);
        var subscribers = snapshot.Tokens.Values
            .Where(record => record.Prefs.IsEnabledFor(category))
            .ToArray();

        if (subscribers.Length == 0)
        {
            return new DispatchSummary(0, 0, 0);
        }

        var removedTokens = new List<string>();
        var deliveredCount = 0;
        var nowIso = DateTimeOffset.UtcNow.ToString("O");

        foreach (var subscriber in subscribers)
        {
            PushSendResult result;
            if (string.Equals(subscriber.Env, "windows-wns", StringComparison.OrdinalIgnoreCase))
            {
                result = await _wnsNotificationSender.SendToastAsync(
                    subscriber.Token,
                    payload,
                    cancellationToken
                );
            }
            else
            {
                _logger.LogInformation(
                    "Skipping unsupported push environment '{Environment}'.",
                    subscriber.Env
                );
                continue;
            }

            if (result.Status == PushSendStatus.Delivered)
            {
                deliveredCount += 1;
            }
            else if (result.Status == PushSendStatus.InvalidChannel)
            {
                removedTokens.Add(subscriber.Token);
            }
        }

        if (deliveredCount > 0 || removedTokens.Count > 0)
        {
            await _stateStore.UpdateAsync(
                state =>
                {
                    foreach (var subscriber in subscribers)
                    {
                        if (state.Tokens.TryGetValue(subscriber.Token, out var current)
                            && !removedTokens.Contains(subscriber.Token, StringComparer.Ordinal))
                        {
                            current.LastNotifiedAt = nowIso;
                            current.UpdatedAt = nowIso;
                        }
                    }

                    foreach (var token in removedTokens)
                    {
                        state.Tokens.Remove(token);
                    }
                },
                cancellationToken
            );
        }

        return new DispatchSummary(subscribers.Length, deliveredCount, removedTokens.Count);
    }
}
