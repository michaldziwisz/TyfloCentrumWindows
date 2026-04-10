using Microsoft.Extensions.DependencyInjection;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.UI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTyfloCentrumUi(this IServiceCollection services)
    {
        services.AddSingleton<ContentFavoriteService>();
        services.AddSingleton<ContentTypeAnnouncementPreferenceService>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<PodcastCatalogViewModel>();
        services.AddTransient<ArticleCatalogViewModel>();
        services.AddTransient<NewsFeedViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<RadioViewModel>();
        services.AddTransient<FavoritesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<FeedbackSectionViewModel>();
        services.AddTransient<TyfloSwiatMagazineViewModel>();
        services.AddTransient<TyfloSwiatPageDetailViewModel>();
        services.AddTransient<CommentDetailViewModel>();
        services.AddTransient<PodcastCommentComposerViewModel>();
        services.AddTransient<ContactTextMessageViewModel>();
        services.AddTransient<ContactVoiceMessageViewModel>();
        return services;
    }
}
