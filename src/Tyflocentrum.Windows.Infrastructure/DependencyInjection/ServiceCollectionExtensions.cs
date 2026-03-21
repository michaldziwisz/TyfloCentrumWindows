using Microsoft.Extensions.DependencyInjection;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.Infrastructure.Http;
using Tyflocentrum.Windows.Infrastructure.Playback;
using Tyflocentrum.Windows.Infrastructure.Storage;

namespace Tyflocentrum.Windows.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTyflocentrumInfrastructure(
        this IServiceCollection services,
        TyflocentrumEndpointsOptions endpoints
    )
    {
        services.AddSingleton(endpoints);
        services.AddSingleton<ILocalSettingsStore, FileLocalSettingsStore>();
        services.AddSingleton<IAppSettingsService, LocalAppSettingsService>();
        services.AddSingleton<IPlaybackResumeService, LocalPlaybackResumeService>();
        services.AddSingleton<IFavoritesService, FileFavoritesService>();
        services.AddSingleton<IAudioPlaybackRequestFactory, AudioPlaybackRequestFactory>();
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
