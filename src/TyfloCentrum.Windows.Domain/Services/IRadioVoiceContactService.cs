using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IRadioVoiceContactService
{
    Task<VoiceMessageSubmissionResult> SendVoiceMessageAsync(
        string author,
        string filePath,
        int durationMs,
        CancellationToken cancellationToken = default
    );
}
