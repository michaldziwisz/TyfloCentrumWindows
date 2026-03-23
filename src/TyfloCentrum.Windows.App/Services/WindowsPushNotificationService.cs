using Microsoft.Extensions.Logging;
using Microsoft.Windows.PushNotifications;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsPushNotificationService
{
    private const string LastRegisteredChannelUriKey = "push.wns.lastRegisteredChannelUri";

    private readonly IAppSettingsService _appSettingsService;
    private readonly ILocalSettingsStore _localSettingsStore;
    private readonly IPushNotificationRegistrationSyncService _pushRegistrationSyncService;
    private readonly ILogger<WindowsPushNotificationService> _logger;
    private readonly TyfloCentrumEndpointsOptions _options;
    private bool _started;

    public WindowsPushNotificationService(
        IAppSettingsService appSettingsService,
        ILocalSettingsStore localSettingsStore,
        IPushNotificationRegistrationSyncService pushRegistrationSyncService,
        ILogger<WindowsPushNotificationService> logger,
        TyfloCentrumEndpointsOptions options
    )
    {
        _appSettingsService = appSettingsService;
        _localSettingsStore = localSettingsStore;
        _pushRegistrationSyncService = pushRegistrationSyncService;
        _logger = logger;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        _started = true;

        if (!PushNotificationManager.IsSupported())
        {
            _logger.LogWarning("Push notifications are not supported for this app/runtime.");
            return;
        }

        PushNotificationManager.Default.PushReceived += OnPushReceived;
        PushNotificationManager.Default.Register();
        await SyncRegistrationIfPossibleAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_started || !PushNotificationManager.IsSupported())
        {
            return Task.CompletedTask;
        }

        PushNotificationManager.Default.PushReceived -= OnPushReceived;
        _started = false;
        return Task.CompletedTask;
    }

    public async Task SyncRegistrationIfPossibleAsync(CancellationToken cancellationToken = default)
    {
        var settings = (await _appSettingsService.GetAsync(cancellationToken)).Normalize();
        var preferences = PushNotificationPreferences.FromSettings(settings);
        var lastRegisteredChannelUri = await _localSettingsStore.GetStringAsync(
            LastRegisteredChannelUriKey,
            cancellationToken
        );

        if (!preferences.AnyEnabled)
        {
            if (!string.IsNullOrWhiteSpace(lastRegisteredChannelUri))
            {
                try
                {
                    await _pushRegistrationSyncService.UnregisterAsync(
                        lastRegisteredChannelUri,
                        cancellationToken
                    );
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Failed to unregister WNS channel from push service."
                    );
                }
            }

            await _localSettingsStore.DeleteStringAsync(LastRegisteredChannelUriKey, cancellationToken);
            return;
        }

        if (!Guid.TryParse(_options.PushAzureObjectId, out var remoteId))
        {
            _logger.LogInformation(
                "WNS push synchronization skipped because PushAzureObjectId is not configured."
            );
            return;
        }

        try
        {
            var result = await PushNotificationManager.Default.CreateChannelAsync(remoteId);
            if (
                result.Status != PushNotificationChannelStatus.CompletedSuccess
                || result.Channel is null
                || string.IsNullOrWhiteSpace(result.Channel.Uri?.ToString())
            )
            {
                _logger.LogWarning(
                    "WNS channel request did not complete successfully. Status: {Status}, Error: {Error}",
                    result.Status,
                    result.ExtendedError
                );
                return;
            }

            var channelUri = result.Channel.Uri.ToString();
            await _pushRegistrationSyncService.RegisterAsync(
                channelUri,
                "windows-wns",
                preferences,
                cancellationToken
            );
            await _localSettingsStore.SetStringAsync(
                LastRegisteredChannelUriKey,
                channelUri,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to synchronize WNS channel with push service.");
        }
    }

    private void OnPushReceived(
        PushNotificationManager sender,
        PushNotificationReceivedEventArgs args
    )
    {
        _logger.LogInformation("Received raw WNS push payload while the app was running.");
    }
}
