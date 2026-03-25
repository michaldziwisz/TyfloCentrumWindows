using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IFeedbackDiagnosticsCollector
{
    Task<FeedbackDiagnosticsSnapshot> CollectAsync(
        bool includeDiagnostics,
        bool includeLogFile,
        CancellationToken cancellationToken = default
    );
}
