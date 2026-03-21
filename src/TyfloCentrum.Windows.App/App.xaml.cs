using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.App.Views;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.DependencyInjection;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.UI.DependencyInjection;
using Windows.Globalization;

namespace TyfloCentrum.Windows.App;

public partial class App : Application
{
    private readonly IHost _host;
    private Window? _mainWindow;

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
            })
            .ConfigureServices((context, services) =>
            {
                var endpoints =
                    context.Configuration.GetSection("Endpoints").Get<TyfloCentrumEndpointsOptions>()
                    ?? new TyfloCentrumEndpointsOptions();

                services.AddTyfloCentrumUi();
                services.AddTyfloCentrumInfrastructure(endpoints);
                services.AddSingleton<WindowHandleProvider>();
                services.AddSingleton<IAudioDeviceCatalogService, WindowsAudioDeviceCatalogService>();
                services.AddSingleton<IClipboardService, WindowsClipboardService>();
                services.AddSingleton<IExternalLinkLauncher, WindowsExternalLinkLauncher>();
                services.AddSingleton<IShareService, WindowsShareService>();
                services.AddTransient<IVoiceMessageRecorder, WindowsVoiceMessageRecorder>();
                services.AddSingleton<AudioPlayerDialogService>();
                services.AddSingleton<ContentEntryActionService>();
                services.AddSingleton<ContactTextMessageDialogService>();
                services.AddSingleton<ContactVoiceMessageDialogService>();
                services.AddSingleton<InAppBrowserDialogService>();
                services.AddSingleton<PostDetailDialogService>();
                services.AddSingleton<CommentDetailDialogService>();
                services.AddSingleton<TyfloSwiatMagazineDialogService>();
                services.AddSingleton<TyfloSwiatPageDetailDialogService>();
                services.AddTransient<NewsSectionView>();
                services.AddTransient<PodcastSectionView>();
                services.AddTransient<ArticleSectionView>();
                services.AddTransient<SearchSectionView>();
                services.AddTransient<FavoritesSectionView>();
                services.AddTransient<RadioSectionView>();
                services.AddTransient<SettingsSectionView>();
                services.AddTransient<TyfloSwiatMagazineView>();
                services.AddTransient<TyfloSwiatPageDetailView>();
                services.AddTransient<AudioPlayerView>();
                services.AddTransient<InAppBrowserView>();
                services.AddTransient<ContactTextMessageView>();
                services.AddTransient<ContactVoiceMessageView>();
                services.AddTransient<PostDetailView>();
                services.AddTransient<CommentDetailView>();
                services.AddSingleton<ShellPage>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow ??= _host.Services.GetRequiredService<MainWindow>();
        _mainWindow.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogError(e.Exception, "Unhandled exception surfaced in the application bootstrap.");
    }
}
