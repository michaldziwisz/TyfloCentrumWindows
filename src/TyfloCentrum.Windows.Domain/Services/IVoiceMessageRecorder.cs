using System.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IVoiceMessageRecorder : INotifyPropertyChanged, IDisposable
{
    event EventHandler<VoiceMessageRecorderNotificationEventArgs>? NotificationRaised;

    VoiceMessageRecorderState State { get; }

    TimeSpan Elapsed { get; }

    int SegmentCount { get; }

    int RecordedDurationMs { get; }

    string? RecordedFilePath { get; }

    bool HasRecording { get; }

    Task<VoiceMessageOperationResult> EnsureMicrophoneAccessAsync(
        CancellationToken cancellationToken = default
    );

    Task<VoiceMessageOperationResult> StartRecordingAsync(
        CancellationToken cancellationToken = default
    );

    Task<VoiceMessageOperationResult> StartAppendingAsync(
        CancellationToken cancellationToken = default
    );

    Task<VoiceMessageOperationResult> StopRecordingAsync(
        CancellationToken cancellationToken = default
    );

    Task<VoiceMessageOperationResult> TogglePreviewAsync(
        CancellationToken cancellationToken = default
    );

    Task StopPreviewAsync(CancellationToken cancellationToken = default);

    Task DeleteRecordingAsync(CancellationToken cancellationToken = default);
}
