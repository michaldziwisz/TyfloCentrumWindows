using Microsoft.Extensions.DependencyInjection;
using Tyflocentrum.Windows.UI.Services;
using Tyflocentrum.Windows.UI.ViewModels;

namespace Tyflocentrum.Windows.UI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTyflocentrumUi(this IServiceCollection services)
    {
        services.AddSingleton<ContentFavoriteService>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<PodcastCatalogViewModel>();
        services.AddTransient<ArticleCatalogViewModel>();
        services.AddTransient<NewsFeedViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<RadioViewModel>();
        services.AddTransient<FavoritesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<TyfloSwiatMagazineViewModel>();
        services.AddTransient<TyfloSwiatPageDetailViewModel>();
        services.AddTransient<PostDetailViewModel>();
        services.AddTransient<CommentDetailViewModel>();
        services.AddTransient<ContactTextMessageViewModel>();
        services.AddTransient<ContactVoiceMessageViewModel>();
        return services;
    }
}
