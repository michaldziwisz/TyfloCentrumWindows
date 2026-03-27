using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.UI.Xaml;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.App.Views;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.DependencyInjection;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.UI.DependencyInjection;
using TyfloCentrum.Windows.UI.Services;
using Windows.Globalization;

namespace TyfloCentrum.Windows.App;

public partial class App : Application
{
    private readonly IHost _host;
    private Window? _mainWindow;
    private IContentNotificationMonitor? _contentNotificationMonitor;
    private WindowsPushNotificationService? _windowsPushNotificationService;
    private bool _appNotificationsRegistered;
    private InternalStoreScreenshotCoordinator? _internalStoreScreenshotCoordinator;

    public App()
    {
        InitializeComponent();
        ApplicationLanguages.PrimaryLanguageOverride = "pl-PL";

        UnhandledException += OnUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddProvider(new RollingFileLoggerProvider());
            })
            .ConfigureServices((context, services) =>
            {
                var endpoints =
                    context.Configuration.GetSection("Endpoints").Get<TyfloCentrumEndpointsOptions>()
                    ?? new TyfloCentrumEndpointsOptions();

                services.AddTyfloCentrumUi();
                services.AddTyfloCentrumInfrastructure(endpoints);
                services.AddSingleton<WindowHandleProvider>();
                services.AddSingleton<InternalStoreScreenshotCoordinator>();
                services.AddSingleton<WindowsDownloadDirectoryService>();
                services.AddSingleton<IFeedbackDiagnosticsCollector, WindowsFeedbackDiagnosticsCollector>();
                services.AddSingleton<NotificationActivationService>();
                services.AddSingleton<IDownloadDirectoryService>(serviceProvider =>
                    serviceProvider.GetRequiredService<WindowsDownloadDirectoryService>()
                );
                services.AddSingleton<IAudioDeviceCatalogService, WindowsAudioDeviceCatalogService>();
                services.AddSingleton<IClipboardService, WindowsClipboardService>();
                services.AddSingleton<IContentNotificationPresenter, WindowsToastNotificationService>();
                services.AddSingleton<IExternalLinkLauncher, WindowsExternalLinkLauncher>();
                services.AddSingleton<IShareService, WindowsShareService>();
                services.AddSingleton<WindowsPushNotificationService>();
                services.AddTransient<IVoiceMessageRecorder, WindowsVoiceMessageRecorder>();
                services.AddSingleton<AudioPlayerDialogService>();
                services.AddSingleton<ContentEntryActionService>();
                services.AddSingleton<ContactTextMessageDialogService>();
                services.AddSingleton<ContactVoiceMessageDialogService>();
                services.AddSingleton<InAppBrowserDialogService>();
                services.AddSingleton<CommentDetailDialogService>();
                services.AddTransient<PodcastShowNotesDialogService>();
                services.AddSingleton<TyfloSwiatMagazineDialogService>();
                services.AddSingleton<TyfloSwiatPageDetailDialogService>();
                services.AddTransient<NewsSectionView>();
                services.AddTransient<PodcastSectionView>();
                services.AddTransient<ArticleSectionView>();
                services.AddTransient<SearchSectionView>();
                services.AddTransient<FavoritesSectionView>();
                services.AddTransient<RadioSectionView>();
                services.AddTransient<SettingsSectionView>();
                services.AddTransient<FeedbackSectionView>();
                services.AddTransient<TyfloSwiatMagazineView>();
                services.AddTransient<TyfloSwiatPageDetailView>();
                services.AddTransient<AudioPlayerView>();
                services.AddTransient<InAppBrowserView>();
                services.AddTransient<ContactTextMessageView>();
                services.AddTransient<ContactVoiceMessageView>();
                services.AddTransient<CommentDetailView>();
                services.AddTransient<PodcastShowNotesDialogView>();
                services.AddSingleton<ShellPage>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow ??= _host.Services.GetRequiredService<MainWindow>();
        _mainWindow.Closed -= OnMainWindowClosed;
        _mainWindow.Closed += OnMainWindowClosed;
        _mainWindow.Activate();
        InitializeUiPreferences();
        HandleInitialActivation();
        if (TryStartInternalStoreScreenshotMode())
        {
            return;
        }

        StartBackgroundServices();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogError(e.Exception, "Unhandled exception surfaced in the application bootstrap.");
    }

    private async void StartBackgroundServices()
    {
        try
        {
            if (!_appNotificationsRegistered)
            {
                AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
                AppNotificationManager.Default.Register();
                _appNotificationsRegistered = true;
            }

            _windowsPushNotificationService ??=
                _host.Services.GetRequiredService<WindowsPushNotificationService>();
            await _windowsPushNotificationService.StartAsync();

            _contentNotificationMonitor ??=
                _host.Services.GetRequiredService<IContentNotificationMonitor>();
            await _contentNotificationMonitor.StartAsync();
        }
        catch (Exception exception)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogError(exception, "Failed to start background content notification monitor.");
        }
    }

    private async void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        if (_contentNotificationMonitor is null)
        {
            if (_appNotificationsRegistered)
            {
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                AppNotificationManager.Default.Unregister();
                _appNotificationsRegistered = false;
            }

            return;
        }

        try
        {
            await _contentNotificationMonitor.StopAsync();
            if (_windowsPushNotificationService is not null)
            {
                await _windowsPushNotificationService.StopAsync();
            }

            if (_appNotificationsRegistered)
            {
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                AppNotificationManager.Default.Unregister();
                _appNotificationsRegistered = false;
            }
        }
        catch (Exception exception)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogError(exception, "Failed to stop background content notification monitor.");
        }
    }

    private void OnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args
    )
    {
        _host.Services.GetRequiredService<NotificationActivationService>().HandleArguments(args.Argument);
        _mainWindow?.Activate();
    }

    private void HandleInitialActivation()
    {
        var activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (
            activationArguments.Kind == ExtendedActivationKind.AppNotification
            && activationArguments.Data is AppNotificationActivatedEventArgs appNotificationArgs
        )
        {
            _host.Services.GetRequiredService<NotificationActivationService>()
                .HandleArguments(appNotificationArgs.Argument);
        }
    }

    private bool TryStartInternalStoreScreenshotMode()
    {
        _internalStoreScreenshotCoordinator ??=
            _host.Services.GetRequiredService<InternalStoreScreenshotCoordinator>();

        if (!_internalStoreScreenshotCoordinator.HasPendingRequest() || _mainWindow is not MainWindow mainWindow)
        {
            return false;
        }

        RunInternalStoreScreenshotModeAsync(mainWindow);
        return true;
    }

    private async void InitializeUiPreferences()
    {
        try
        {
            var appSettingsService = _host.Services.GetRequiredService<IAppSettingsService>();
            var contentTypeAnnouncementPreferenceService =
                _host.Services.GetRequiredService<ContentTypeAnnouncementPreferenceService>();
            await contentTypeAnnouncementPreferenceService.InitializeAsync(appSettingsService);
        }
        catch (Exception exception)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogError(exception, "Failed to initialize UI accessibility preferences.");
        }
    }

    private async void RunInternalStoreScreenshotModeAsync(MainWindow mainWindow)
    {
        try
        {
            await _internalStoreScreenshotCoordinator!.TryRunAsync(mainWindow);
        }
        catch (Exception exception)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogError(exception, "Failed to run internal store screenshot mode.");
            Exit();
        }
    }
}
