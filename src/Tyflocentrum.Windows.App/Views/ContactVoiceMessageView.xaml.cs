using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using System.ComponentModel;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Tyflocentrum.Windows.UI.ViewModels;

namespace Tyflocentrum.Windows.App.Views;

public sealed partial class ContactVoiceMessageView : UserControl
{
    private static readonly Uri RecordingCueUri = new("ms-appx:///Assets/recording-cue.wav");
    private const int RecordingCueAnnouncementDelayMs = 1200;
    private const int RecordingCuePostSignalDelayMs = 60;
    private const int RecordingCueFallbackTimeoutMs = 1200;

    private bool _holdToTalkPointerActive;
    private bool _isStartingWithCue;
    private string? _lastAnnouncedStatusMessage;
    private MediaPlayer? _cuePlayer;
    private MediaPlayer? _previewPlayer;

    public ContactVoiceMessageView(ContactVoiceMessageViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisualState();
    }

    public ContactVoiceMessageViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Focus(FocusState.Programmatic);
    }

    private async void OnStartRecordingClick(object sender, RoutedEventArgs e)
    {
        await StartRecordingWithCueAsync(append: false);
    }

    private async void OnAppendRecordingClick(object sender, RoutedEventArgs e)
    {
        await StartRecordingWithCueAsync(append: true);
    }

    private async void OnStopRecordingClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.StopRecordingAsync();
    }

    private async void OnHoldToTalkPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_holdToTalkPointerActive || _isStartingWithCue || ViewModel.IsSending || ViewModel.IsRecording)
        {
            return;
        }

        _holdToTalkPointerActive = true;
        HoldToTalkButton.CapturePointer(e.Pointer);

        await ViewModel.StartHoldToTalkAsync();

        if (!ViewModel.IsRecording)
        {
            _holdToTalkPointerActive = false;
            HoldToTalkButton.ReleasePointerCapture(e.Pointer);
        }
    }

    private async void OnHoldToTalkPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        await CompleteHoldToTalkAsync(e.Pointer);
    }

    private async void OnHoldToTalkPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        await CompleteHoldToTalkAsync(e.Pointer);
    }

    private async void OnHoldToTalkPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        await CompleteHoldToTalkAsync(e.Pointer);
    }

    private async void OnTogglePreviewClick(object sender, RoutedEventArgs e)
    {
        var wasPlayingPreview = ViewModel.IsPlayingPreview;
        await ViewModel.TogglePreviewAsync();

        if (!ViewModel.IsPlayingPreview)
        {
            if (wasPlayingPreview)
            {
                StopPreviewPlayer();
            }

            return;
        }

        try
        {
            var filePath = ViewModel.RecordedFilePath;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                await ViewModel.FailPreviewAsync();
                return;
            }

            var previewFile = await StorageFile.GetFileFromPathAsync(filePath);
            var previewPlayer = EnsurePreviewPlayer();
            previewPlayer.Source = null;
            previewPlayer.Source = MediaSource.CreateFromStorageFile(previewFile);
            previewPlayer.Play();
        }
        catch
        {
            StopPreviewPlayer();
            await ViewModel.FailPreviewAsync();
        }
    }

    private async void OnDeleteRecordingClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.DeleteRecordingAsync();
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SendAsync();
    }

    private async void OnRecordShortcutInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        if (ViewModel.IsRecording)
        {
            await ViewModel.StopRecordingAsync();
        }
        else
        {
            await StartRecordingWithCueAsync(append: ViewModel.HasRecording);
        }

        args.Handled = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContactVoiceMessageViewModel.StatusMessage))
        {
            UpdateVisualState();
            AnnounceStatusMessage(ViewModel.StatusMessage, _isStartingWithCue);
        }
        else
        {
            UpdateVisualState();
        }

        if (!ViewModel.IsPlayingPreview)
        {
            StopPreviewPlayer();
        }
    }

    private void UpdateVisualState()
    {
        StatusTextBlock.Text = ViewModel.StatusMessage ?? string.Empty;
        StatusTextBlock.Visibility = ViewModel.HasStatus ? Visibility.Visible : Visibility.Collapsed;

        ErrorBar.IsOpen = ViewModel.HasError;
        ErrorBar.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.Message = ViewModel.ErrorMessage;

        var controlsEnabled = !_isStartingWithCue;
        StartButton.Content = ViewModel.StartButtonText;
        StartButton.IsEnabled = controlsEnabled && ViewModel.CanStartRecording;
        AppendButton.Content = ViewModel.AppendButtonText;
        AppendButton.IsEnabled = controlsEnabled && ViewModel.CanAppendRecording;
        HoldToTalkButton.Content = ViewModel.HoldToTalkButtonText;
        HoldToTalkButton.IsEnabled =
            controlsEnabled
            &&
            !ViewModel.IsSending
            && !ViewModel.IsPreparing
            && (!string.IsNullOrWhiteSpace(ViewModel.Name.Trim()) || ViewModel.IsRecording);
        StopButton.IsEnabled = controlsEnabled && ViewModel.CanStopRecording;
        PreviewButton.IsEnabled = controlsEnabled && ViewModel.CanTogglePreview;
        PreviewButton.Content = ViewModel.PreviewButtonText;
        DeleteButton.IsEnabled = controlsEnabled && ViewModel.CanDeleteRecording;
        SendButton.IsEnabled = controlsEnabled && ViewModel.CanSend;
        SendingIndicator.IsActive = ViewModel.IsSending;
        SendingIndicator.Visibility = ViewModel.IsSending ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task CompleteHoldToTalkAsync(Pointer pointer)
    {
        if (!_holdToTalkPointerActive)
        {
            return;
        }

        _holdToTalkPointerActive = false;
        HoldToTalkButton.ReleasePointerCapture(pointer);

        if (ViewModel.IsRecording)
        {
            await ViewModel.StopHoldToTalkAsync();
        }
    }

    private void OnPreviewMediaEnded(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() => _ = CompletePreviewAsync());
    }

    private void OnPreviewMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() => _ = FailPreviewAsync());
    }

    private async Task CompletePreviewAsync()
    {
        StopPreviewPlayer();
        await ViewModel.FinishPreviewAsync();
    }

    private async Task FailPreviewAsync()
    {
        StopPreviewPlayer();
        await ViewModel.FailPreviewAsync();
    }

    private void StopPreviewPlayer()
    {
        if (_previewPlayer is null)
        {
            return;
        }

        _previewPlayer.Pause();
        _previewPlayer.Source = null;
    }

    private MediaPlayer EnsurePreviewPlayer()
    {
        if (_previewPlayer is not null)
        {
            return _previewPlayer;
        }

        _previewPlayer = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media,
        };
        _previewPlayer.MediaEnded += OnPreviewMediaEnded;
        _previewPlayer.MediaFailed += OnPreviewMediaFailed;
        PreviewPlayerElement.SetMediaPlayer(_previewPlayer);
        return _previewPlayer;
    }

    private async Task StartRecordingWithCueAsync(bool append)
    {
        if (_isStartingWithCue || ViewModel.IsRecording || ViewModel.IsPreparing || ViewModel.IsSending)
        {
            return;
        }

        _isStartingWithCue = true;

        try
        {
            var microphoneAccessResult = await ViewModel.EnsureMicrophoneAccessAsync();
            if (!microphoneAccessResult.Success)
            {
                return;
            }

            ViewModel.AnnounceRecordingStartCue(append);
            UpdateVisualState();
            await Task.Delay(RecordingCueAnnouncementDelayMs);
            await PlayRecordingSignalAsync();
            await Task.Delay(RecordingCuePostSignalDelayMs);

            if (append)
            {
                await ViewModel.StartAppendingAfterCueAsync();
            }
            else
            {
                await ViewModel.StartRecordingAfterCueAsync();
            }
        }
        finally
        {
            _isStartingWithCue = false;
            UpdateVisualState();
        }
    }

    private async Task PlayRecordingSignalAsync()
    {
        TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var cuePlayer = _cuePlayer ??= new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.SoundEffects,
            Source = MediaSource.CreateFromUri(RecordingCueUri),
        };

        void OnMediaEnded(MediaPlayer sender, object args)
        {
            completionSource.TrySetResult();
        }

        void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            completionSource.TrySetResult();
        }

        cuePlayer.MediaEnded += OnMediaEnded;
        cuePlayer.MediaFailed += OnMediaFailed;

        try
        {
            cuePlayer.Pause();
            cuePlayer.PlaybackSession.Position = TimeSpan.Zero;
            cuePlayer.Play();
            await Task.WhenAny(
                completionSource.Task,
                Task.Delay(RecordingCueFallbackTimeoutMs)
            );
        }
        catch
        {
            // Best effort only. Lack of system beep must not block recording.
        }
        finally
        {
            cuePlayer.MediaEnded -= OnMediaEnded;
            cuePlayer.MediaFailed -= OnMediaFailed;
            cuePlayer.Pause();
            cuePlayer.PlaybackSession.Position = TimeSpan.Zero;
        }
    }

    private void AnnounceStatusMessage(string? message, bool isImportant)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _lastAnnouncedStatusMessage = null;
            return;
        }

        if (string.Equals(_lastAnnouncedStatusMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastAnnouncedStatusMessage = message;
        AutomationProperties.SetName(StatusTextBlock, message);

        var peer =
            FrameworkElementAutomationPeer.FromElement(StatusTextBlock)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(StatusTextBlock);

        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            isImportant
                ? AutomationNotificationProcessing.ImportantMostRecent
                : AutomationNotificationProcessing.MostRecent,
            message,
            "ContactVoiceMessage.Status"
        );
    }
}
