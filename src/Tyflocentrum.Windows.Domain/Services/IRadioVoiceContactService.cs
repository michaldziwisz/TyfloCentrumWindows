using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IRadioVoiceContactService
{
    Task<VoiceMessageSubmissionResult> SendVoiceMessageAsync(
        string author,
        string filePath,
        int durationMs,
        CancellationToken cancellationToken = default
    );
}
