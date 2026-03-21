using System.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContactVoiceMessageViewModelTests
{
    [Fact]
    public async Task StartRecordingAsync_requires_name_before_recording()
    {
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            new FakeVoiceMessageRecorder(),
            new FakeLocalSettingsStore()
        );

        await viewModel.StartRecordingAsync();

        Assert.Equal("Uzupełnij imię, aby nagrać głosówkę.", viewModel.ErrorMessage);
        Assert.False(viewModel.IsRecording);
    }

    [Fact]
    public void AnnounceRecordingStartCue_sets_status_for_new_recording()
    {
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            new FakeVoiceMessageRecorder(),
            new FakeLocalSettingsStore()
        );

        viewModel.AnnounceRecordingStartCue(append: false);

        Assert.Equal("Nagraj wiadomość po sygnale.", viewModel.StatusMessage);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void AnnounceRecordingStartCue_sets_status_for_append()
    {
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            new FakeVoiceMessageRecorder(),
            new FakeLocalSettingsStore()
        );

        viewModel.AnnounceRecordingStartCue(append: true);

        Assert.Equal("Dograj fragment po sygnale.", viewModel.StatusMessage);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task StartRecordingAsync_surfaces_microphone_permission_error()
    {
        var recorder = new FakeVoiceMessageRecorder
        {
            StartRecordingResult = new VoiceMessageOperationResult(
                false,
                "Brak dostępu do mikrofonu. Windows powinien teraz wyświetlić systemowe pytanie o zgodę."
            ),
        };
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartRecordingAsync();

        Assert.Equal(
            "Brak dostępu do mikrofonu. Windows powinien teraz wyświetlić systemowe pytanie o zgodę.",
            viewModel.ErrorMessage
        );
        Assert.Equal(viewModel.ErrorMessage, viewModel.StatusMessage);
        Assert.False(viewModel.IsRecording);
    }

    [Fact]
    public async Task StopRecordingAsync_updates_state_after_successful_recording()
    {
        var recorder = new FakeVoiceMessageRecorder();
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartRecordingAsync();
        await viewModel.StopRecordingAsync();

        Assert.True(viewModel.HasRecording);
        Assert.Equal("Nagranie zapisane. Możesz je odsłuchać lub wysłać.", viewModel.StatusMessage);
        Assert.Equal("0:04", viewModel.DurationText);
    }

    [Fact]
    public async Task StopRecordingAsync_updates_start_button_when_recording_exists()
    {
        var recorder = new FakeVoiceMessageRecorder();
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartRecordingAsync();
        await viewModel.StopRecordingAsync();

        Assert.True(viewModel.HasRecording);
        Assert.Equal("Nagraj od nowa", viewModel.StartButtonText);
    }

    [Fact]
    public async Task StartHoldToTalkAsync_updates_recording_state_and_button_text()
    {
        var recorder = new FakeVoiceMessageRecorder();
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartHoldToTalkAsync();

        Assert.True(viewModel.IsRecording);
        Assert.Equal("Puść, aby zakończyć", viewModel.HoldToTalkButtonText);
        Assert.Equal(0, recorder.AppendStartCount);
    }

    [Fact]
    public async Task StartHoldToTalkAsync_appends_when_recording_already_exists()
    {
        var recorder = new FakeVoiceMessageRecorder();
        recorder.SeedRecording(@"D:\temp\voice.m4a", 5300, segmentCount: 1);
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartHoldToTalkAsync();

        Assert.True(viewModel.IsRecording);
        Assert.Equal(1, recorder.AppendStartCount);
    }

    [Fact]
    public async Task StopRecordingAsync_after_append_updates_segment_count_and_status()
    {
        var recorder = new FakeVoiceMessageRecorder();
        recorder.SeedRecording(@"D:\temp\voice.m4a", 5300, segmentCount: 1);
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartAppendingAsync();
        await viewModel.StopRecordingAsync();

        Assert.True(viewModel.HasRecording);
        Assert.Equal(2, viewModel.SegmentCount);
        Assert.Equal("2 fragmenty", viewModel.SegmentCountText);
        Assert.Equal(
            "Fragment dodany. Nagranie gotowe do odsłuchu lub wysłania.",
            viewModel.StatusMessage
        );
    }

    [Fact]
    public async Task StopRecordingAsync_when_recording_is_not_active_does_not_surface_error()
    {
        var recorder = new FakeVoiceMessageRecorder();
        recorder.SeedRecording(@"D:\temp\voice.m4a", 5300, segmentCount: 1);
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
            StatusMessage = "Nagranie zapisane. Możesz je odsłuchać lub wysłać.",
        };

        await viewModel.StopRecordingAsync();

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Nagranie zapisane. Możesz je odsłuchać lub wysłać.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task StopRecordingAsync_ignores_concurrent_second_stop_request()
    {
        var recorder = new FakeVoiceMessageRecorder
        {
            StopRecordingDelay = TimeSpan.FromMilliseconds(50),
        };
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartRecordingAsync();

        var firstStop = viewModel.StopRecordingAsync();
        var secondStop = viewModel.StopRecordingAsync();

        await Task.WhenAll(firstStop, secondStop);

        Assert.Equal(1, recorder.StopRecordingCallCount);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Nagranie zapisane. Możesz je odsłuchać lub wysłać.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Recorder_notification_updates_error_and_status_after_interruption()
    {
        var recorder = new FakeVoiceMessageRecorder();
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.StartRecordingAsync();
        recorder.SimulateInterruption(
            "Mikrofon przestał być dostępny. Nagrywanie zostało zatrzymane."
        );

        Assert.Equal(
            "Mikrofon przestał być dostępny. Nagrywanie zostało zatrzymane.",
            viewModel.ErrorMessage
        );
        Assert.Equal(
            "Mikrofon przestał być dostępny. Nagrywanie zostało zatrzymane.",
            viewModel.StatusMessage
        );
        Assert.False(viewModel.IsRecording);
    }

    [Fact]
    public async Task TogglePreviewAsync_sets_status_for_preview_start_and_stop()
    {
        var recorder = new FakeVoiceMessageRecorder();
        recorder.SeedRecording(@"D:\temp\voice.m4a", 5300);
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        await viewModel.TogglePreviewAsync();
        Assert.True(viewModel.IsPlayingPreview);
        Assert.Equal("Odtwarzanie nagrania.", viewModel.StatusMessage);

        await viewModel.TogglePreviewAsync();
        Assert.False(viewModel.IsPlayingPreview);
        Assert.Equal("Odtwarzanie podglądu zakończone.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task StartRecordingAfterCueAsync_clears_cue_status_after_successful_start()
    {
        var recorder = new FakeVoiceMessageRecorder();
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        viewModel.AnnounceRecordingStartCue(append: false);
        await viewModel.StartRecordingAfterCueAsync();

        Assert.True(viewModel.IsRecording);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task StartAppendingAfterCueAsync_clears_cue_status_after_successful_start()
    {
        var recorder = new FakeVoiceMessageRecorder();
        recorder.SeedRecording(@"D:\temp\voice.m4a", 5300, segmentCount: 1);
        var viewModel = new ContactVoiceMessageViewModel(
            new FakeRadioVoiceContactService(),
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };

        viewModel.AnnounceRecordingStartCue(append: true);
        await viewModel.StartAppendingAfterCueAsync();

        Assert.True(viewModel.IsRecording);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task SendAsync_uses_recorder_file_and_raises_sent_event()
    {
        var recorder = new FakeVoiceMessageRecorder();
        recorder.SeedRecording(@"D:\temp\voice.m4a", 5300);
        var service = new FakeRadioVoiceContactService
        {
            Result = new VoiceMessageSubmissionResult(true, null, 5300),
        };
        var viewModel = new ContactVoiceMessageViewModel(
            service,
            recorder,
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
        };
        var wasSent = false;
        viewModel.MessageSent += (_, _) => wasSent = true;

        var result = await viewModel.SendAsync();

        Assert.True(result);
        Assert.True(wasSent);
        Assert.Equal("UI", service.LastAuthor);
        Assert.Equal(@"D:\temp\voice.m4a", service.LastFilePath);
        Assert.Equal(5300, service.LastDurationMs);
        Assert.Equal("Głosówka wysłana pomyślnie.", viewModel.StatusMessage);
        Assert.False(viewModel.HasRecording);
    }

    private sealed class FakeRadioVoiceContactService : IRadioVoiceContactService
    {
        public VoiceMessageSubmissionResult Result { get; init; } = new(true, null, 0);

        public string? LastAuthor { get; private set; }

        public string? LastFilePath { get; private set; }

        public int LastDurationMs { get; private set; }

        public Task<VoiceMessageSubmissionResult> SendVoiceMessageAsync(
            string author,
            string filePath,
            int durationMs,
            CancellationToken cancellationToken = default
        )
        {
            LastAuthor = author;
            LastFilePath = filePath;
            LastDurationMs = durationMs;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeVoiceMessageRecorder : IVoiceMessageRecorder
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<VoiceMessageRecorderNotificationEventArgs>? NotificationRaised;

        public VoiceMessageRecorderState State { get; private set; }

        public TimeSpan Elapsed { get; private set; }

        public int SegmentCount { get; private set; }

        public int RecordedDurationMs { get; private set; }

        public string? RecordedFilePath { get; private set; }

        public int AppendStartCount { get; private set; }

        public bool HasRecording => !string.IsNullOrWhiteSpace(RecordedFilePath) && SegmentCount > 0;

        public VoiceMessageOperationResult StartRecordingResult { get; set; } = new(true, null);

        public VoiceMessageOperationResult StartAppendingResult { get; set; } = new(true, null);

        public VoiceMessageOperationResult EnsureMicrophoneAccessResult { get; set; } = new(true, null);

        public int StopRecordingCallCount { get; private set; }

        public TimeSpan StopRecordingDelay { get; set; }

        private bool NextRecordingIsAppend { get; set; }

        public Task<VoiceMessageOperationResult> EnsureMicrophoneAccessAsync(
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(EnsureMicrophoneAccessResult);
        }

        public Task<VoiceMessageOperationResult> StartRecordingAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (!StartRecordingResult.Success)
            {
                return Task.FromResult(StartRecordingResult);
            }

            NextRecordingIsAppend = false;
            State = VoiceMessageRecorderState.Recording;
            Elapsed = TimeSpan.Zero;
            RaiseChanged();
            return Task.FromResult(new VoiceMessageOperationResult(true, null));
        }

        public Task<VoiceMessageOperationResult> StartAppendingAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (!StartAppendingResult.Success)
            {
                return Task.FromResult(StartAppendingResult);
            }

            AppendStartCount++;
            NextRecordingIsAppend = true;
            State = VoiceMessageRecorderState.Recording;
            Elapsed = TimeSpan.FromMilliseconds(RecordedDurationMs);
            RaiseChanged();
            return Task.FromResult(new VoiceMessageOperationResult(true, null));
        }

        public Task<VoiceMessageOperationResult> StopRecordingAsync(
            CancellationToken cancellationToken = default
        )
        {
            StopRecordingCallCount++;
            return StopRecordingCoreAsync(cancellationToken);
        }

        private async Task<VoiceMessageOperationResult> StopRecordingCoreAsync(
            CancellationToken cancellationToken
        )
        {
            if (StopRecordingDelay > TimeSpan.Zero)
            {
                await Task.Delay(StopRecordingDelay, cancellationToken);
            }

            if (NextRecordingIsAppend && !string.IsNullOrWhiteSpace(RecordedFilePath))
            {
                SegmentCount = Math.Max(1, SegmentCount) + 1;
                RecordedDurationMs += 4200;
            }
            else
            {
                RecordedFilePath = @"D:\temp\voice.m4a";
                RecordedDurationMs = 4200;
                SegmentCount = 1;
            }

            Elapsed = TimeSpan.FromMilliseconds(RecordedDurationMs);
            State = VoiceMessageRecorderState.Recorded;
            NextRecordingIsAppend = false;
            RaiseChanged();
            return new VoiceMessageOperationResult(true, null);
        }

        public Task<VoiceMessageOperationResult> TogglePreviewAsync(
            CancellationToken cancellationToken = default
        )
        {
            State = State == VoiceMessageRecorderState.PlayingPreview
                ? VoiceMessageRecorderState.Recorded
                : VoiceMessageRecorderState.PlayingPreview;
            RaiseChanged();
            return Task.FromResult(new VoiceMessageOperationResult(true, null));
        }

        public Task StopPreviewAsync(CancellationToken cancellationToken = default)
        {
            State = SegmentCount > 0 ? VoiceMessageRecorderState.Recorded : VoiceMessageRecorderState.Idle;
            RaiseChanged();
            return Task.CompletedTask;
        }

        public Task DeleteRecordingAsync(CancellationToken cancellationToken = default)
        {
            State = VoiceMessageRecorderState.Idle;
            Elapsed = TimeSpan.Zero;
            SegmentCount = 0;
            RecordedDurationMs = 0;
            RecordedFilePath = null;
            NextRecordingIsAppend = false;
            RaiseChanged();
            return Task.CompletedTask;
        }

        public void SeedRecording(string filePath, int durationMs, int segmentCount = 1)
        {
            RecordedFilePath = filePath;
            RecordedDurationMs = durationMs;
            SegmentCount = segmentCount;
            Elapsed = TimeSpan.FromMilliseconds(durationMs);
            State = VoiceMessageRecorderState.Recorded;
            RaiseChanged();
        }

        public void Dispose()
        {
        }

        public void SimulateInterruption(string message)
        {
            State = SegmentCount > 0 ? VoiceMessageRecorderState.Recorded : VoiceMessageRecorderState.Idle;
            RaiseChanged();
            NotificationRaised?.Invoke(
                this,
                new VoiceMessageRecorderNotificationEventArgs(message, true)
            );
        }

        private void RaiseChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Elapsed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SegmentCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecordedDurationMs)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecordedFilePath)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasRecording)));
        }
    }

    private sealed class FakeLocalSettingsStore : ILocalSettingsStore
    {
        public ValueTask<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask SetStringAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteStringAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
