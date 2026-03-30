using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsToastNotificationService : IContentNotificationPresenter
{
    private readonly IAppRuntimeMode _appRuntimeMode;

    public WindowsToastNotificationService(IAppRuntimeMode appRuntimeMode)
    {
        _appRuntimeMode = appRuntimeMode;
    }

    public Task ShowNewContentAsync(
        ContentSource source,
        WpPostSummary item,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_appRuntimeMode.SupportsSystemNotifications)
        {
            return Task.CompletedTask;
        }

        var body = WordPressContentText.NormalizeHtml(item.Excerpt?.Rendered ?? string.Empty);
        if (string.IsNullOrWhiteSpace(body))
        {
            body = "Otwórz aplikację TyfloCentrum, aby przejść do nowej treści.";
        }

        var notification = new AppNotificationBuilder()
            .AddArgument("kind", source == ContentSource.Podcast ? "podcast" : "article")
            .AddArgument("id", item.Id.ToString())
            .AddArgument("title", WordPressContentText.NormalizeHtml(item.Title.Rendered))
            .AddArgument("date", item.Date)
            .AddArgument("link", item.Link)
            .AddText(source == ContentSource.Podcast ? "Nowy podcast" : "Nowy artykuł")
            .AddText(WordPressContentText.NormalizeHtml(item.Title.Rendered))
            .AddText(body)
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
        return Task.CompletedTask;
    }
}
