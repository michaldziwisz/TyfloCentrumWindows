using Microsoft.Extensions.DependencyInjection;
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
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IPushNotificationRegistrationSyncService, PushNotificationRegistrationSyncService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IContentDownloadService, ContentDownloadService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddHttpClient<INewsFeedService, WordPressNewsFeedService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IWordPressCatalogService, WordPressCatalogService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IWordPressSearchService, WordPressSearchService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IWordPressPostDetailsService, WordPressPostDetailsService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<ITyfloSwiatMagazineService, WordPressTyfloSwiatMagazineService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IWordPressCommentsService, WordPressCommentsService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddTransient<IPodcastShowNotesService, PodcastShowNotesService>();
        services.AddHttpClient<IRadioService, ContactPanelRadioService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IRadioContactService, ContactPanelRadioContactService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IRadioVoiceContactService, ContactPanelRadioVoiceContactService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        return services;
    }
}
