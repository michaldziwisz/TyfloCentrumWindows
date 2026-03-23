using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IContentNotificationPresenter
{
    Task ShowNewContentAsync(
        ContentSource source,
        WpPostSummary item,
        CancellationToken cancellationToken = default
    );
}
