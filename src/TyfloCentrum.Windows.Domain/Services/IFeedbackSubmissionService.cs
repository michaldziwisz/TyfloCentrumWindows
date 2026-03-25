using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IFeedbackSubmissionService
{
    Task<FeedbackSubmissionResult> SubmitAsync(
        FeedbackSubmissionRequest request,
        CancellationToken cancellationToken = default
    );
}
