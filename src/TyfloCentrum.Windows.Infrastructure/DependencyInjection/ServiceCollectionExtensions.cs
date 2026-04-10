using Microsoft.Extensions.DependencyInjection;
using System.Net;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Notifications;
using TyfloCentrum.Windows.Infrastructure.Playback;
using TyfloCentrum.Windows.Infrastructure.Storage;

namespace TyfloCentrum.Windows.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTyfloCentrumInfrastructure(
        this IServiceCollection services,
        TyfloCentrumEndpointsOptions endpoints
    )
    {
        services.AddSingleton(endpoints);
        services.AddSingleton<ILocalSettingsStore, FileLocalSettingsStore>();
        services.AddSingleton<ITransientContentCache, FileBackedTransientContentCache>();
        services.AddSingleton<IAppSettingsService, LocalAppSettingsService>();
        services.AddSingleton<IContentNotificationStateStore, LocalContentNotificationStateStore>();
        services.AddSingleton<IContentNotificationMonitor, ContentNotificationMonitor>();
        services.AddSingleton<IPlaybackResumeService, LocalPlaybackResumeService>();
        services.AddSingleton<IFavoritesService, FileFavoritesService>();
        services.AddSingleton<IAudioPlaybackRequestFactory, AudioPlaybackRequestFactory>();
        services.AddHttpClient<IFeedbackSubmissionService, SygnalistaFeedbackSubmissionService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IPushNotificationRegistrationSyncService, PushNotificationRegistrationSyncService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IContentDownloadService, ContentDownloadService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            TyfloCentrumHttpClientDefaults.EnsureUserAgent(client);
        });
        services.AddHttpClient<INewsFeedService, WordPressNewsFeedService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IWordPressCatalogService, WordPressCatalogService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IWordPressSearchService, WordPressSearchService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IWordPressPostDetailsService, WordPressPostDetailsService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<ITyfloSwiatMagazineService, WordPressTyfloSwiatMagazineService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IWordPressCommentsService, WordPressCommentsService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        }).ConfigurePrimaryHttpMessageHandler(static () =>
            new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression =
                    DecompressionMethods.GZip
                    | DecompressionMethods.Deflate
                    | DecompressionMethods.Brotli,
            });
        services.AddTransient<IPodcastShowNotesService, PodcastShowNotesService>();
        services.AddHttpClient<IRadioService, ContactPanelRadioService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IRadioContactService, ContactPanelRadioContactService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(30));
        });
        services.AddHttpClient<IRadioVoiceContactService, ContactPanelRadioVoiceContactService>(client =>
        {
            TyfloCentrumHttpClientDefaults.ConfigureJsonClient(client, TimeSpan.FromSeconds(60));
        });
        return services;
    }
}
