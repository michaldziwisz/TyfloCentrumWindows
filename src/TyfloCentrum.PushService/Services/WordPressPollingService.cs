using TyfloCentrum.PushService.Options;
using Microsoft.Extensions.Options;

namespace TyfloCentrum.PushService.Services;

public sealed class WordPressPollingService : BackgroundService
{
    private readonly WordPressPollingCoordinator _coordinator;
    private readonly PushServiceOptions _options;
    private readonly ILogger<WordPressPollingService> _logger;

    public WordPressPollingService(
        WordPressPollingCoordinator coordinator,
        IOptions<PushServiceOptions> options,
        ILogger<WordPressPollingService> logger
    )
    {
        _coordinator = coordinator;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _coordinator.PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Push-service polling iteration failed.");
            }

            var delay = TimeSpan.FromSeconds(Math.Max(60, _options.PollIntervalSeconds));
            await Task.Delay(delay, stoppingToken);
        }
    }
}
