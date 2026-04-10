using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Casting;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI.Popups;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;
using TyfloCentrum.Windows.UI.ViewModels;
using WinRT.Interop;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class AudioPlayerView : UserControl
{
    private const double ResumePersistIntervalSeconds = 5d;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;
    private readonly IClipboardService _clipboardService;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFavoritesService _favoritesService;
    private readonly IPlaybackResumeService _playbackResumeService;
    private readonly PodcastCommentComposerViewModel _commentComposerViewModel;
    private readonly IShareService _shareService;
    private readonly WindowHandleProvider _windowHandleProvider;
    private readonly IWordPressCommentsService _wordPressCommentsService;
    private readonly PlaybackRateOption[] _playbackRates;
    private CancellationTokenSource? _showNotesLoadCts;
    private PodcastChapterMarkerItemViewModel[] _chapterMarkers = [];
    private CommentItemViewModel[] _comments = [];
    private PodcastRelatedLinkItemViewModel[] _relatedLinks = [];
    private Button? _addCommentButton;
    private ListView? _chapterMarkersListView;
    private ListView? _commentsListView;
    private ListView? _relatedLinksListView;
    private TextBox? _commentAuthorNameTextBox;
    private TextBox? _commentAuthorEmailTextBox;
    private TextBox? _commentContentTextBox;
    private MediaPlayer? _mediaPlayer;
    private AppSettingsSnapshot _currentSettings = AppSettingsSnapshot.Defaults;
    private bool _isChapterMarkersVisible;
    private bool _isCommentsVisible;
    private bool _isCommentComposerVisible;
    private bool _isLoadingShowNotes;
    private bool _isRelatedLinksVisible;
    private bool _isSynchronizingCommentComposer;
    private bool _isSynchronizingPositionSlider;
    private bool _isTransportUiRefreshPending;
    private bool _hasPlaybackEnded;
    private bool _isRestoringResumePosition;
    private bool _isSynchronizingVolume;
    private bool _isDisconnectingCasting;
    private double _lastPersistedResumeSeconds;
    private double _lastKnownDurationSeconds;
    private AudioPlaybackRequest? _currentRequest;
    private CastingConnection? _castingConnection;
    private CastingDevicePicker? _castingDevicePicker;
    private double? _pendingResumePositionSeconds;

    public AudioPlayerView(
        IAppSettingsService appSettingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService,
        IClipboardService clipboardService,
        IFavoritesService favoritesService,
        IPlaybackResumeService playbackResumeService,
        PodcastCommentComposerViewModel commentComposerViewModel,
        IShareService shareService,
        IWordPressCommentsService wordPressCommentsService,
        IExternalLinkLauncher externalLinkLauncher,
        WindowHandleProvider windowHandleProvider
    )
    {
        _appSettingsService = appSettingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
        _clipboardService = clipboardService;
        _favoritesService = favoritesService;
        _playbackResumeService = playbackResumeService;
        _commentComposerViewModel = commentComposerViewModel;
        _shareService = shareService;
        _wordPressCommentsService = wordPressCommentsService;
        _externalLinkLauncher = externalLinkLauncher;
        _windowHandleProvider = windowHandleProvider;
        _commentComposerViewModel.PropertyChanged += OnCommentComposerPropertyChanged;
        _playbackRates = PlaybackRateCatalog
            .SupportedValues.Select(value => new PlaybackRateOption(PlaybackRateCatalog.FormatLabel(value), value))
            .ToArray();
        InitializeComponent();
        PlaybackRateComboBox.ItemsSource = _playbackRates;
        PlaybackRateComboBox.DisplayMemberPath = nameof(PlaybackRateOption.Label);
        PlaybackRateComboBox.SelectedItem = _playbackRates.First(option =>
            option.Value == PlaybackRateCatalog.DefaultValue
        );
        SetVolume(AppSettingsSnapshot.DefaultPlaybackVolumePercent, announce: false);
    }

    public async Task InitializeAsync(
        AudioPlaybackRequest request,
        CancellationToken cancellationToken = default
    )
    {
        _currentRequest = null;
        _pendingResumePositionSeconds = null;
        _lastPersistedResumeSeconds = 0;
        _lastKnownDurationSeconds = 0;
        _hasPlaybackEnded = false;
        _isRestoringResumePosition = false;
        ErrorBar.IsOpen = false;
        ErrorBar.Visibility = Visibility.Collapsed;
        ErrorBar.Message = string.Empty;
        OutputDeviceTextBlock.Text = string.Empty;
        OutputDeviceTextBlock.Visibility = Visibility.Collapsed;
        ResetShowNotesState();

        SourceTypeTextBlock.Text = request.SourceTypeLabel;
        TitleTextBlock.Text = request.Title;
        SubtitleTextBlock.Text = request.Subtitle ?? string.Empty;
        SubtitleTextBlock.Visibility = string.IsNullOrWhiteSpace(request.Subtitle)
            ? Visibility.Collapsed
            : Visibility.Visible;

        PodcastControlsPanel.Visibility = request.IsLive ? Visibility.Collapsed : Visibility.Visible;
        PlaybackRateComboBox.Visibility = request.CanChangePlaybackRate
            ? Visibility.Visible
            : Visibility.Collapsed;
        ShortcutHelpPanel.Visibility = Visibility.Visible;
        ConfigureShortcutHelpText(request);

        _currentSettings = (await _appSettingsService.GetAsync(cancellationToken)).Normalize();
        ConfigurePlaybackRateSelection(request);
        SetVolume(_currentSettings.EffectivePlaybackVolumePercent, announce: false);
        ConfigureTransportControls(request);
        await ConfigureMediaPlayerAsync(request, cancellationToken);

        if (!request.IsLive && request.PodcastPostId is int podcastPostId)
        {
            _commentComposerViewModel.Initialize(podcastPostId);
            _commentComposerViewModel.CancelReply();
            await _commentComposerViewModel.LoadIfNeededAsync(cancellationToken);
            _showNotesLoadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = LoadShowNotesAsync(podcastPostId, _showNotesLoadCts.Token);
        }
    }

    public async Task StopAndDisposePlayerAsync()
    {
        CancelShowNotesLoad();

        if (_mediaPlayer is null)
        {
        _currentRequest = null;
        _pendingResumePositionSeconds = null;
        _lastPersistedResumeSeconds = 0;
        _lastKnownDurationSeconds = 0;
        _hasPlaybackEnded = false;
        _isRestoringResumePosition = false;
        await DisconnectCastingAsync(announce: false);
        return;
        }

        var mediaPlayer = _mediaPlayer;
        _mediaPlayer = null;
        await PersistCurrentPositionIfNeededAsync(mediaPlayer);
        DetachMediaPlayer(mediaPlayer);
        mediaPlayer.Pause();
        mediaPlayer.Source = null;
        PlayerElement.SetMediaPlayer(null);
        mediaPlayer.Dispose();
        _currentRequest = null;
        _pendingResumePositionSeconds = null;
        _lastPersistedResumeSeconds = 0;
        _lastKnownDurationSeconds = 0;
        _hasPlaybackEnded = false;
        _isRestoringResumePosition = false;
        await DisconnectCastingAsync(announce: false);
    }

    private async Task ConfigureMediaPlayerAsync(
        AudioPlaybackRequest request,
        CancellationToken cancellationToken
    )
    {
        await StopAndDisposePlayerAsync();
        _currentRequest = request;
        _pendingResumePositionSeconds = null;
        _lastPersistedResumeSeconds = 0;
        _lastKnownDurationSeconds = 0;
        _hasPlaybackEnded = false;
        _isRestoringResumePosition = false;

        var mediaPlayer = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media,
            Source = MediaSource.CreateFromUri(request.SourceUrl),
            Volume = VolumeSlider.Value / 100d,
        };

        mediaPlayer.MediaFailed += OnMediaFailed;
        mediaPlayer.MediaOpened += OnMediaOpened;
        mediaPlayer.MediaEnded += OnMediaEnded;
        mediaPlayer.PlaybackSession.PositionChanged += OnPlaybackSessionPositionChanged;
        mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackSessionPlaybackStateChanged;
        try
        {
            var outputDeviceMessage = await TryApplyOutputDeviceAsync(
                mediaPlayer,
                _currentSettings.PreferredOutputDeviceId,
                cancellationToken
            );

            PlayerElement.SetMediaPlayer(mediaPlayer);
            _mediaPlayer = mediaPlayer;
            _pendingResumePositionSeconds =
                request.IsLive || !request.CanSeek
                    ? null
                    : request.InitialSeekSeconds is double initialSeekSeconds && initialSeekSeconds > 1d
                        ? initialSeekSeconds
                    : await _playbackResumeService.GetResumePositionAsync(
                        request.SourceUrl,
                        cancellationToken
                    );
            _lastPersistedResumeSeconds = _pendingResumePositionSeconds ?? 0;

            if (
                request.CanChangePlaybackRate
                && PlaybackRateComboBox.SelectedItem is PlaybackRateOption rate
            )
            {
                mediaPlayer.PlaybackRate = rate.Value;
            }

            mediaPlayer.Play();

            if (!string.IsNullOrWhiteSpace(outputDeviceMessage))
            {
                OutputDeviceTextBlock.Text = outputDeviceMessage;
                OutputDeviceTextBlock.Visibility = Visibility.Visible;
            }

            QueueTransportControlsRefresh();
            SetStatusMessage(
                request.IsLive
                    ? "Trwa odtwarzanie transmisji na żywo."
                    : "Odtwarzanie podcastu. Możesz przewijać i zmieniać prędkość."
            );
        }
        catch
        {
            DetachMediaPlayer(mediaPlayer);
            mediaPlayer.Dispose();
            throw;
        }
    }

    private void ConfigureTransportControls(AudioPlaybackRequest request)
    {
        TransportControlsPanel.Visibility = Visibility.Visible;
        SkipBackwardButton.Visibility = request.CanSeek ? Visibility.Visible : Visibility.Collapsed;
        SkipForwardButton.Visibility = request.CanSeek ? Visibility.Visible : Visibility.Collapsed;
        SeekControlsPanel.Visibility = request.CanSeek ? Visibility.Visible : Visibility.Collapsed;
        DurationTextBlock.Text = request.CanSeek ? "--:--" : "Na żywo";
        CurrentPositionTextBlock.Text = request.CanSeek ? "00:00" : "Na żywo";
        _isSynchronizingPositionSlider = true;
        PositionSlider.Minimum = 0d;
        PositionSlider.Maximum = 1d;
        PositionSlider.Value = 0d;
        PositionSlider.IsEnabled = request.CanSeek;
        _isSynchronizingPositionSlider = false;
        UpdatePlayPauseButton(false);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var controlPressed = KeyboardShortcutHelper.IsControlPressed();
        var altPressed = KeyboardShortcutHelper.IsAltPressed();
        if (!controlPressed && !altPressed)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Space when controlPressed:
                e.Handled = true;
                TogglePlayback();
                break;
            case VirtualKey.Left when controlPressed && _currentRequest?.CanSeek == true:
                e.Handled = true;
                SeekBy(TimeSpan.FromSeconds(-30), "Przewinięto wstecz o 30 sekund.");
                break;
            case VirtualKey.Right when controlPressed && _currentRequest?.CanSeek == true:
                e.Handled = true;
                SeekBy(TimeSpan.FromSeconds(30), "Przewinięto do przodu o 30 sekund.");
                break;
            case VirtualKey.Up when altPressed:
                e.Handled = true;
                AdjustPlaybackRate(1);
                break;
            case VirtualKey.Down when altPressed:
                e.Handled = true;
                AdjustPlaybackRate(-1);
                break;
            case VirtualKey.Up when controlPressed:
                e.Handled = true;
                AdjustVolume(1);
                break;
            case VirtualKey.Down when controlPressed:
                e.Handled = true;
                AdjustVolume(-1);
                break;
            case VirtualKey.D when controlPressed:
                e.Handled = true;
                _ = ToggleFocusedShowNotesFavoriteAsync();
                break;
        }
    }

    private void OnTogglePlaybackAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        TogglePlayback();
        args.Handled = true;
    }

    private async void OnToggleFavoriteAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        args.Handled = true;
        await ToggleFocusedShowNotesFavoriteAsync();
    }

    private void OnSkipBackwardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        if (_currentRequest?.CanSeek == true)
        {
            SeekBy(TimeSpan.FromSeconds(-30), "Przewinięto wstecz o 30 sekund.");
        }

        args.Handled = true;
    }

    private void OnSkipForwardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        if (_currentRequest?.CanSeek == true)
        {
            SeekBy(TimeSpan.FromSeconds(30), "Przewinięto do przodu o 30 sekund.");
        }

        args.Handled = true;
    }

    private void OnIncreasePlaybackRateAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        AdjustPlaybackRate(1);
        args.Handled = true;
    }

    private void OnDecreasePlaybackRateAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        AdjustPlaybackRate(-1);
        args.Handled = true;
    }

    private void OnIncreaseVolumeAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        AdjustVolume(1);
        args.Handled = true;
    }

    private void OnDecreaseVolumeAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        AdjustVolume(-1);
        args.Handled = true;
    }

    private async void OnPlaybackRateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_mediaPlayer is null || PlaybackRateComboBox.SelectedItem is not PlaybackRateOption option)
        {
            return;
        }

        _mediaPlayer.PlaybackRate = option.Value;
        SetStatusMessage($"Prędkość odtwarzania: {option.Label}.");

        if (!_currentSettings.RememberLastPlaybackRate)
        {
            return;
        }

        _currentSettings = _currentSettings with { LastPlaybackRate = option.Value };

        try
        {
            await _appSettingsService.SaveAsync(_currentSettings);
        }
        catch
        {
            SetStatusMessage(
                $"Prędkość odtwarzania: {option.Label}. Nie udało się zapisać tej preferencji."
            );
        }
    }

    private async void OnVolumeSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        var percent = Math.Clamp(e.NewValue, 0d, 100d);
        VolumeValueTextBlock.Text = $"{percent:0}%";

        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = percent / 100d;
        }

        if (_isSynchronizingVolume)
        {
            return;
        }

        SetStatusMessage($"Głośność: {percent:0}%.");

        if (!_currentSettings.RememberLastPlaybackVolume)
        {
            return;
        }

        _currentSettings = _currentSettings with { LastPlaybackVolumePercent = percent };

        try
        {
            await _appSettingsService.SaveAsync(_currentSettings);
        }
        catch
        {
            SetStatusMessage(
                $"Głośność: {percent:0}%. Nie udało się zapisać tej preferencji."
            );
        }
    }

    private void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        TogglePlayback();
    }

    private void OnSkipBackwardButtonClick(object sender, RoutedEventArgs e)
    {
        if (_currentRequest?.CanSeek == true)
        {
            SeekBy(TimeSpan.FromSeconds(-30), "Przewinięto wstecz o 30 sekund.");
        }
    }

    private void OnSkipForwardButtonClick(object sender, RoutedEventArgs e)
    {
        if (_currentRequest?.CanSeek == true)
        {
            SeekBy(TimeSpan.FromSeconds(30), "Przewinięto do przodu o 30 sekund.");
        }
    }

    private async void OnCastToDeviceButtonClick(object sender, RoutedEventArgs e)
    {
        if (_castingConnection is not null && _castingConnection.State != CastingConnectionState.Disconnected)
        {
            await DisconnectCastingAsync(announce: true);
            return;
        }

        if (_mediaPlayer is null)
        {
            SetStatusMessage("Odtwarzacz nie jest jeszcze gotowy do przesyłania dźwięku.", announce: true);
            return;
        }

        if (_windowHandleProvider.Handle == IntPtr.Zero)
        {
            SetStatusMessage("Nie udało się otworzyć wyboru urządzenia zewnętrznego.", announce: true);
            return;
        }

        try
        {
            var picker = EnsureCastingDevicePicker();
            picker.Filter.SupportsAudio = true;
            picker.Filter.SupportsVideo = false;
            picker.Filter.SupportsPictures = false;
            picker.Filter.SupportedCastingSources.Clear();
            picker.Filter.SupportedCastingSources.Add(_mediaPlayer.GetAsCastingSource());

            var anchor = CastToDeviceButton.TransformToVisual(null).TransformPoint(new Point(0, 0));
            var rect = new Rect(anchor.X, anchor.Y, Math.Max(1, CastToDeviceButton.ActualWidth), Math.Max(1, CastToDeviceButton.ActualHeight));
            picker.Show(rect, Placement.Above);
            SetStatusMessage("Wybierz urządzenie zewnętrzne do przesyłania dźwięku.", announce: true);
        }
        catch
        {
            SetStatusMessage("Nie udało się otworzyć wyboru urządzenia zewnętrznego.", announce: true, important: true);
        }
    }

    private void OnPositionSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (
            _isSynchronizingPositionSlider
            || _currentRequest?.CanSeek != true
            || _mediaPlayer?.PlaybackSession is not { } session
        )
        {
            return;
        }

        var targetSeconds = Math.Max(0d, e.NewValue);
        var durationSeconds =
            session.NaturalDuration.TotalSeconds > 0d
                ? session.NaturalDuration.TotalSeconds
                : _lastKnownDurationSeconds;
        if (durationSeconds > 1d)
        {
            targetSeconds = Math.Min(targetSeconds, durationSeconds);
        }

        session.Position = TimeSpan.FromSeconds(targetSeconds);
        QueueTransportControlsRefresh();
    }

    private void SeekBy(TimeSpan delta, string announcement)
    {
        if (_mediaPlayer?.PlaybackSession is not { } session)
        {
            return;
        }

        var targetPosition = session.Position + delta;
        if (targetPosition < TimeSpan.Zero)
        {
            targetPosition = TimeSpan.Zero;
        }

        var naturalDuration = session.NaturalDuration;
        if (naturalDuration > TimeSpan.Zero && targetPosition > naturalDuration)
        {
            targetPosition = naturalDuration;
        }

        session.Position = targetPosition;
        QueueTransportControlsRefresh();
        SetStatusMessage(announcement);
    }

    private void TogglePlayback()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        if (_mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            _mediaPlayer.Pause();
            UpdatePlayPauseButton(isPlaying: false);
            SetStatusMessage(
                _currentRequest?.IsLive == true
                    ? "Wstrzymano transmisję na żywo."
                    : "Wstrzymano odtwarzanie."
            );
            return;
        }

        _mediaPlayer.Play();
        UpdatePlayPauseButton(isPlaying: true);
        SetStatusMessage(
            _currentRequest?.IsLive == true
                ? "Wznowiono transmisję na żywo."
                : "Wznowiono odtwarzanie."
        );
    }

    private void AdjustPlaybackRate(int direction)
    {
        if (
            _mediaPlayer is null
            || _currentRequest?.CanChangePlaybackRate != true
            || PlaybackRateComboBox.SelectedItem is not PlaybackRateOption currentOption
        )
        {
            return;
        }

        var currentIndex = Array.FindIndex(_playbackRates, option => option.Value == currentOption.Value);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var targetIndex = Math.Clamp(currentIndex + direction, 0, _playbackRates.Length - 1);
        if (targetIndex == currentIndex)
        {
            SetStatusMessage(
                direction < 0
                    ? "To jest najniższa dostępna prędkość odtwarzania."
                    : "To jest najwyższa dostępna prędkość odtwarzania."
            );
            return;
        }

        PlaybackRateComboBox.SelectedItem = _playbackRates[targetIndex];
    }

    private void AdjustVolume(int direction)
    {
        var step = VolumeSlider.StepFrequency > 0d ? VolumeSlider.StepFrequency : 5d;
        var currentValue = VolumeSlider.Value;
        var targetValue = Math.Clamp(currentValue + (direction * step), 0d, 100d);

        if (Math.Abs(targetValue - currentValue) < 0.01d)
        {
            SetStatusMessage(
                direction < 0
                    ? "To jest najniższa dostępna głośność."
                    : "To jest najwyższa dostępna głośność."
            );
            return;
        }

        VolumeSlider.Value = targetValue;
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
        {
            return "--:--";
        }

        var value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ErrorBar.Message = "Nie udało się odtworzyć audio. Sprawdź połączenie i spróbuj ponownie.";
            ErrorBar.IsOpen = true;
            ErrorBar.Visibility = Visibility.Visible;
            UpdatePlayPauseButton(isPlaying: false);
            QueueTransportControlsRefresh();
            SetStatusMessage(ErrorBar.Message);
        });
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        if (_pendingResumePositionSeconds is not double resumePositionSeconds || resumePositionSeconds <= 1d)
        {
            QueueTransportControlsRefresh();
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_mediaPlayer?.PlaybackSession is not { } session)
            {
                return;
            }

            var targetSeconds = resumePositionSeconds;
            var naturalDuration = session.NaturalDuration.TotalSeconds;
            if (naturalDuration > 1d)
            {
                targetSeconds = Math.Min(targetSeconds, Math.Max(0d, naturalDuration - 1d));
            }

            if (targetSeconds <= 1d)
            {
                QueueTransportControlsRefresh();
                return;
            }

            _isRestoringResumePosition = true;
            session.Position = TimeSpan.FromSeconds(targetSeconds);
            _lastPersistedResumeSeconds = targetSeconds;
            _pendingResumePositionSeconds = null;
            _isRestoringResumePosition = false;
            QueueTransportControlsRefresh();
            var wasInitialSeekRequested =
                _currentRequest?.InitialSeekSeconds is double initialSeekSeconds
                && initialSeekSeconds > 1d;
            SetStatusMessage(
                wasInitialSeekRequested
                    ? $"Rozpoczęto od {FormatTime(targetSeconds)}."
                    : $"Wznowiono od {FormatTime(targetSeconds)}."
            );
        });
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        if (_currentRequest is null || _currentRequest.IsLive)
        {
            return;
        }

        _hasPlaybackEnded = true;
        _pendingResumePositionSeconds = null;
        _lastPersistedResumeSeconds = 0;
        _lastKnownDurationSeconds = 0;
        QueueTransportControlsRefresh();
        _ = ClearResumePositionAsync(_currentRequest.SourceUrl);
    }

    private void OnPlaybackSessionPositionChanged(MediaPlaybackSession sender, object args)
    {
        if (
            _mediaPlayer is null
            || _currentRequest is null
            || _currentRequest.IsLive
            || _hasPlaybackEnded
            || _isRestoringResumePosition
        )
        {
            return;
        }

        var positionSeconds = sender.Position.TotalSeconds;
        if (positionSeconds <= 1d)
        {
            return;
        }

        if (
            _lastPersistedResumeSeconds > 0d
            && Math.Abs(positionSeconds - _lastPersistedResumeSeconds) < ResumePersistIntervalSeconds
        )
        {
            return;
        }

        _lastPersistedResumeSeconds = positionSeconds;
        QueueTransportControlsRefresh();
        _ = PersistResumePositionAsync(_currentRequest.SourceUrl, positionSeconds);
    }

    private void OnPlaybackSessionPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        QueueTransportControlsRefresh();
    }

    private void SetStatusMessage(string? message, bool announce = false, bool important = false)
    {
        StatusTextBlock.Text = message ?? string.Empty;
        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (announce)
        {
            AutomationAnnouncementHelper.Announce(StatusTextBlock, message, important);
        }
    }

    private void QueueTransportControlsRefresh()
    {
        if (_isTransportUiRefreshPending)
        {
            return;
        }

        _isTransportUiRefreshPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _isTransportUiRefreshPending = false;
            RefreshTransportControlsUi();
        });
    }

    private void RefreshTransportControlsUi()
    {
        var canSeek = _currentRequest?.CanSeek == true;
        SeekControlsPanel.Visibility = canSeek ? Visibility.Visible : Visibility.Collapsed;
        SkipBackwardButton.Visibility = canSeek ? Visibility.Visible : Visibility.Collapsed;
        SkipForwardButton.Visibility = canSeek ? Visibility.Visible : Visibility.Collapsed;

        if (_mediaPlayer?.PlaybackSession is not { } session)
        {
            UpdatePlayPauseButton(isPlaying: false);
            if (canSeek)
            {
                SynchronizePositionSlider(0d, _lastKnownDurationSeconds);
            }
            else
            {
                CurrentPositionTextBlock.Text = "Na żywo";
                DurationTextBlock.Text = "Na żywo";
            }

            return;
        }

        UpdatePlayPauseButton(session.PlaybackState == MediaPlaybackState.Playing);

        if (!canSeek)
        {
            CurrentPositionTextBlock.Text = "Na żywo";
            DurationTextBlock.Text = "Na żywo";
            return;
        }

        var positionSeconds = Math.Max(0d, session.Position.TotalSeconds);
        var durationSeconds = session.NaturalDuration.TotalSeconds;
        if (durationSeconds > 1d)
        {
            _lastKnownDurationSeconds = durationSeconds;
        }
        else
        {
            durationSeconds = _lastKnownDurationSeconds;
        }

        SynchronizePositionSlider(positionSeconds, durationSeconds);
    }

    private void SynchronizePositionSlider(double positionSeconds, double durationSeconds)
    {
        _isSynchronizingPositionSlider = true;
        PositionSlider.Maximum = durationSeconds > 1d ? durationSeconds : Math.Max(1d, positionSeconds);
        PositionSlider.IsEnabled = durationSeconds > 1d;
        PositionSlider.Value = Math.Clamp(positionSeconds, 0d, PositionSlider.Maximum);
        _isSynchronizingPositionSlider = false;

        CurrentPositionTextBlock.Text = FormatTime(positionSeconds);
        DurationTextBlock.Text = durationSeconds > 1d ? FormatTime(durationSeconds) : "--:--";
    }

    private void UpdatePlayPauseButton(bool isPlaying)
    {
        var text = isPlaying ? "Pauza" : "Odtwarzaj";
        PlayPauseButton.Content = text;
        AutomationProperties.SetName(PlayPauseButton, text);
    }

    private CastingDevicePicker EnsureCastingDevicePicker()
    {
        if (_castingDevicePicker is not null)
        {
            return _castingDevicePicker;
        }

        _castingDevicePicker = new CastingDevicePicker();
        InitializeWithWindow.Initialize(_castingDevicePicker, _windowHandleProvider.Handle);
        _castingDevicePicker.CastingDeviceSelected += OnCastingDeviceSelected;
        return _castingDevicePicker;
    }

    private void OnCastingDeviceSelected(CastingDevicePicker sender, CastingDeviceSelectedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() => _ = StartCastingAsync(args.SelectedCastingDevice));
    }

    private async Task StartCastingAsync(CastingDevice device)
    {
        if (_mediaPlayer is null)
        {
            SetStatusMessage("Odtwarzacz nie jest jeszcze gotowy do przesyłania dźwięku.", announce: true);
            return;
        }

        await DisconnectCastingAsync(announce: false);

        var connection = device.CreateCastingConnection();
        connection.StateChanged += OnCastingConnectionStateChanged;
        connection.ErrorOccurred += OnCastingConnectionErrorOccurred;
        _castingConnection = connection;
        UpdateCastingUi();

        try
        {
            var status = await connection.RequestStartCastingAsync(_mediaPlayer.GetAsCastingSource());
            if (status == CastingConnectionErrorStatus.Succeeded)
            {
                SetStatusMessage($"Rozpoczęto przesyłanie do urządzenia: {device.FriendlyName}.", announce: true, important: true);
                UpdateCastingUi();
                return;
            }

            await CleanupCastingConnectionAsync(connection);
            SetStatusMessage(BuildCastingErrorMessage(status), announce: true, important: true);
        }
        catch
        {
            await CleanupCastingConnectionAsync(connection);
            SetStatusMessage("Nie udało się rozpocząć przesyłania dźwięku do urządzenia zewnętrznego.", announce: true, important: true);
        }
    }

    private void OnCastingConnectionStateChanged(CastingConnection sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(_castingConnection, sender))
            {
                return;
            }

            UpdateCastingUi();

            var message = sender.State switch
            {
                CastingConnectionState.Connecting => "Łączenie z urządzeniem zewnętrznym…",
                CastingConnectionState.Connected => "Połączono z urządzeniem zewnętrznym.",
                CastingConnectionState.Rendering => "Dźwięk jest przesyłany do urządzenia zewnętrznego.",
                CastingConnectionState.Disconnecting => "Rozłączanie urządzenia zewnętrznego…",
                CastingConnectionState.Disconnected => "Przesyłanie do urządzenia zewnętrznego zostało zakończone.",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(message))
            {
                SetStatusMessage(message, announce: true);
            }

            if (sender.State == CastingConnectionState.Disconnected && !_isDisconnectingCasting)
            {
                _ = CleanupCastingConnectionAsync(sender);
            }
        });
    }

    private void OnCastingConnectionErrorOccurred(CastingConnection sender, CastingConnectionErrorOccurredEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(_castingConnection, sender))
            {
                return;
            }

            _ = CleanupCastingConnectionAsync(sender);
            SetStatusMessage(BuildCastingErrorMessage(args.ErrorStatus), announce: true, important: true);
        });
    }

    private async Task DisconnectCastingAsync(bool announce)
    {
        if (_castingConnection is null)
        {
            UpdateCastingUi();
            return;
        }

        var connection = _castingConnection;
        _isDisconnectingCasting = true;
        UpdateCastingUi();

        try
        {
            var status = await connection.DisconnectAsync();
            await CleanupCastingConnectionAsync(connection);

            if (announce)
            {
                var message = status == CastingConnectionErrorStatus.Succeeded
                    ? "Rozłączono urządzenie zewnętrzne."
                    : BuildCastingErrorMessage(status);
                SetStatusMessage(message, announce: true, important: status != CastingConnectionErrorStatus.Succeeded);
            }
        }
        catch
        {
            await CleanupCastingConnectionAsync(connection);
            if (announce)
            {
                SetStatusMessage("Nie udało się rozłączyć urządzenia zewnętrznego.", announce: true, important: true);
            }
        }
        finally
        {
            _isDisconnectingCasting = false;
            UpdateCastingUi();
        }
    }

    private Task CleanupCastingConnectionAsync(CastingConnection connection)
    {
        connection.StateChanged -= OnCastingConnectionStateChanged;
        connection.ErrorOccurred -= OnCastingConnectionErrorOccurred;
        connection.Dispose();

        if (ReferenceEquals(_castingConnection, connection))
        {
            _castingConnection = null;
        }

        UpdateCastingUi();
        return Task.CompletedTask;
    }

    private void UpdateCastingUi()
    {
        var isConnected = _castingConnection is not null
            && _castingConnection.State is CastingConnectionState.Connected or CastingConnectionState.Rendering or CastingConnectionState.Connecting or CastingConnectionState.Disconnecting;

        CastToDeviceButton.Content = isConnected ? "Rozłącz urządzenie" : "Przesyłaj do urządzenia";
        AutomationProperties.SetName(
            CastToDeviceButton,
            isConnected
                ? "Rozłącz urządzenie zewnętrzne"
                : "Przesyłaj dźwięk do urządzenia zewnętrznego"
        );
    }

    private static string BuildCastingErrorMessage(CastingConnectionErrorStatus status)
    {
        return status switch
        {
            CastingConnectionErrorStatus.DeviceDidNotRespond => "Urządzenie zewnętrzne nie odpowiedziało.",
            CastingConnectionErrorStatus.DeviceError => "Urządzenie zewnętrzne zgłosiło błąd.",
            CastingConnectionErrorStatus.DeviceLocked => "Urządzenie zewnętrzne jest zablokowane.",
            CastingConnectionErrorStatus.ProtectedPlaybackFailed => "Urządzenie zewnętrzne nie obsługuje tego typu odtwarzania.",
            CastingConnectionErrorStatus.InvalidCastingSource => "Tego dźwięku nie udało się przesłać do urządzenia zewnętrznego.",
            CastingConnectionErrorStatus.Unknown => "Nie udało się przesłać dźwięku do urządzenia zewnętrznego.",
            CastingConnectionErrorStatus.Succeeded => "Przesyłanie do urządzenia zewnętrznego zakończyło się powodzeniem.",
            _ => "Nie udało się przesłać dźwięku do urządzenia zewnętrznego.",
        };
    }

    private async Task LoadShowNotesAsync(int podcastPostId, CancellationToken cancellationToken)
    {
        _isLoadingShowNotes = true;
        UpdateShowNotesUi("Ładowanie komentarzy, znaczników czasu i odnośników…");

        try
        {
            var comments = await _wordPressCommentsService.GetCommentsAsync(
                podcastPostId,
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();

            var commentItems = PodcastCommentThreadBuilder.Build(comments);
            var parsed = ShowNotesParser.Parse(comments);
            var chapterMarkers = parsed.Markers
                .Select(marker => new PodcastChapterMarkerItemViewModel(
                    marker.Title,
                    marker.Seconds,
                    FormatTime(marker.Seconds)
                ))
                .ToArray();
            var relatedLinks = parsed.Links
                .Select(link => new PodcastRelatedLinkItemViewModel(
                    link.Title,
                    link.Url,
                    GetHostLabel(link.Url)
                ))
                .ToArray();

            if (_currentRequest?.PodcastPostId is int currentPodcastPostId)
            {
                chapterMarkers = await LoadChapterMarkerFavoritesAsync(
                    chapterMarkers,
                    currentPodcastPostId,
                    cancellationToken
                );
                relatedLinks = await LoadRelatedLinkFavoritesAsync(
                    relatedLinks,
                    currentPodcastPostId,
                    cancellationToken
                );
            }

            _comments = commentItems;
            _chapterMarkers = chapterMarkers;
            _relatedLinks = relatedLinks;

            _isCommentsVisible = false;
            _isChapterMarkersVisible = false;
            _isRelatedLinksVisible = false;
            _isLoadingShowNotes = false;

            UpdateShowNotesUi(
                _comments.Length == 0 && _chapterMarkers.Length == 0 && _relatedLinks.Length == 0
                    ? null
                    : BuildShowNotesReadyMessage()
            );
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _comments = [];
            _chapterMarkers = [];
            _relatedLinks = [];
            _isLoadingShowNotes = false;
            UpdateShowNotesUi("Nie udało się wczytać komentarzy i dodatków do odcinka.");
        }
    }

    private void ConfigurePlaybackRateSelection(AudioPlaybackRequest request)
    {
        var initialRate = request.CanChangePlaybackRate
            ? _currentSettings.EffectivePlaybackRate
            : PlaybackRateCatalog.DefaultValue;

        PlaybackRateComboBox.SelectedItem = _playbackRates.First(option =>
            option.Value == PlaybackRateCatalog.Coerce(initialRate)
        );
    }

    private void ConfigureShortcutHelpText(AudioPlaybackRequest request)
    {
        ShortcutHelpTextBlock.Text = request.CanChangePlaybackRate
            ? "Skróty: Ctrl+spacja odtwarzaj lub pauzuj, Ctrl+strzałka w lewo i prawo przewijają o 30 sekund, Alt+strzałka w górę i dół zmieniają prędkość, Ctrl+strzałka w górę i dół zmieniają głośność, Ctrl+D przełącza ulubione dla zaznaczonego dodatku odcinka, Ctrl+U udostępnia zaznaczony odnośnik."
            : "Skróty: Ctrl+spacja odtwarzaj lub pauzuj, Ctrl+strzałka w górę i dół zmieniają głośność, Ctrl+D przełącza ulubione dla zaznaczonego dodatku odcinka, Ctrl+U udostępnia zaznaczony odnośnik.";
    }

    private void SetVolume(double percent, bool announce)
    {
        _isSynchronizingVolume = true;
        VolumeSlider.Value = Math.Clamp(percent, 0d, 100d);
        VolumeValueTextBlock.Text = $"{VolumeSlider.Value:0}%";
        _isSynchronizingVolume = false;

        if (announce)
        {
            SetStatusMessage($"Głośność: {VolumeSlider.Value:0}%.");
        }
    }

    private async Task<string?> TryApplyOutputDeviceAsync(
        MediaPlayer mediaPlayer,
        string? preferredOutputDeviceId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(preferredOutputDeviceId))
        {
            return "Urządzenie wyjściowe: domyślne urządzenie systemowe.";
        }

        var outputDevices = await _audioDeviceCatalogService.GetOutputDevicesAsync(cancellationToken);
        var selectedDevice = outputDevices.FirstOrDefault(device =>
            string.Equals(device.Id, preferredOutputDeviceId, StringComparison.Ordinal)
        );

        if (selectedDevice is null)
        {
            throw new InvalidOperationException(
                "Wybrane urządzenie wyjściowe nie jest obecnie dostępne."
            );
        }

        var deviceInformation = await DeviceInformation.CreateFromIdAsync(selectedDevice.Id);
        mediaPlayer.AudioDevice = deviceInformation;
        return $"Urządzenie wyjściowe: {selectedDevice.Name}.";
    }

    private void OnToggleChapterMarkersClick(object sender, RoutedEventArgs e)
    {
        _isChapterMarkersVisible = !_isChapterMarkersVisible;
        if (_isChapterMarkersVisible)
        {
            _isCommentsVisible = false;
            _isRelatedLinksVisible = false;
        }

        UpdateShowNotesUi("Widok znaczników czasu został zaktualizowany.");

        if (_isChapterMarkersVisible)
        {
            FocusChapterMarkersList();
        }
    }

    private void OnToggleRelatedLinksClick(object sender, RoutedEventArgs e)
    {
        _isRelatedLinksVisible = !_isRelatedLinksVisible;
        if (_isRelatedLinksVisible)
        {
            _isCommentsVisible = false;
            _isChapterMarkersVisible = false;
        }

        UpdateShowNotesUi("Widok odnośników został zaktualizowany.");

        if (_isRelatedLinksVisible)
        {
            FocusRelatedLinksList();
        }
    }

    private void OnToggleCommentsClick(object sender, RoutedEventArgs e)
    {
        _isCommentsVisible = !_isCommentsVisible;
        if (_isCommentsVisible)
        {
            _isChapterMarkersVisible = false;
            _isRelatedLinksVisible = false;
        }

        UpdateShowNotesUi("Widok komentarzy został zaktualizowany.");

        if (_isCommentsVisible)
        {
            FocusCommentsList();
        }
    }

    private void OnChapterMarkersListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PodcastChapterMarkerItemViewModel item)
        {
            SeekToMarker(item);
        }
    }

    private void OnChapterMarkersListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (sender is ListView { SelectedItem: PodcastChapterMarkerItemViewModel item })
        {
            e.Handled = true;
            SeekToMarker(item);
        }
    }

    private void OnChapterMarkersListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var item =
            ItemContextResolver.Resolve<PodcastChapterMarkerItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as PodcastChapterMarkerItemViewModel;
        if (item is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var seekItem = new MenuFlyoutItem { Text = "Przejdź" };
        AutomationProperties.SetName(seekItem, $"Przejdź do {item.TimeLabel}");
        seekItem.Click += (_, _) => SeekToMarker(item);
        flyout.Items.Add(seekItem);

        var favoriteItem = new MenuFlyoutItem
        {
            Text = item.IsFavorite
                ? "Usuń z ulubionych (Ctrl+D)"
                : "Dodaj do ulubionych (Ctrl+D)",
        };
        AutomationProperties.SetName(favoriteItem, item.FavoriteMenuLabel);
        favoriteItem.Click += async (_, _) => await ToggleChapterMarkerFavoriteAsync(item);
        flyout.Items.Add(favoriteItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private async void OnRelatedLinksListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PodcastRelatedLinkItemViewModel item)
        {
            await OpenRelatedLinkAsync(item);
        }
    }

    private void OnCommentsListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommentItemViewModel item)
        {
            ToggleCommentDetails(item);
        }
    }

    private void OnCommentsListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (sender is ListView { SelectedItem: CommentItemViewModel item })
        {
            e.Handled = true;
            ToggleCommentDetails(item);
        }
    }

    private void OnCommentsListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var item =
            ItemContextResolver.Resolve<CommentItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as CommentItemViewModel;
        if (item is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var replyItem = new MenuFlyoutItem { Text = item.ReplyButtonText };
        AutomationProperties.SetName(replyItem, item.ReplyButtonLabel);
        replyItem.Click += async (_, _) => await ShowCommentComposerAsync(item);
        flyout.Items.Add(replyItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private async void OnCommentReplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommentItemViewModel item })
        {
            await ShowCommentComposerAsync(item);
        }
    }

    private async void OnRelatedLinksListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (
            KeyboardShortcutHelper.IsControlPressed()
            && sender is ListView { SelectedItem: PodcastRelatedLinkItemViewModel selectedItem }
            && e.Key == VirtualKey.U
        )
        {
            e.Handled = true;
            await ShareRelatedLinkAsync(selectedItem);
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (sender is ListView { SelectedItem: PodcastRelatedLinkItemViewModel item })
        {
            e.Handled = true;
            await OpenRelatedLinkAsync(item);
        }
    }

    private void OnRelatedLinksListContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var item =
            ItemContextResolver.Resolve<PodcastRelatedLinkItemViewModel>(e.OriginalSource)
            ?? listView.SelectedItem as PodcastRelatedLinkItemViewModel;
        if (item is null)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var openItem = new MenuFlyoutItem { Text = "Otwórz odnośnik" };
        AutomationProperties.SetName(openItem, item.OpenMenuLabel);
        openItem.Click += async (_, _) => await OpenRelatedLinkAsync(item);
        flyout.Items.Add(openItem);

        var copyItem = new MenuFlyoutItem { Text = "Kopiuj odnośnik" };
        AutomationProperties.SetName(copyItem, item.CopyMenuLabel);
        copyItem.Click += async (_, _) => await CopyRelatedLinkAsync(item);
        flyout.Items.Add(copyItem);

        var shareItem = new MenuFlyoutItem { Text = "Udostępnij (Ctrl+U)" };
        AutomationProperties.SetName(shareItem, item.ShareMenuLabel);
        shareItem.Click += async (_, _) => await ShareRelatedLinkAsync(item);
        flyout.Items.Add(shareItem);

        var favoriteItem = new MenuFlyoutItem
        {
            Text = item.IsFavorite
                ? "Usuń z ulubionych (Ctrl+D)"
                : "Dodaj do ulubionych (Ctrl+D)",
        };
        AutomationProperties.SetName(favoriteItem, item.FavoriteMenuLabel);
        favoriteItem.Click += async (_, _) => await ToggleRelatedLinkFavoriteAsync(item);
        flyout.Items.Add(favoriteItem);

        flyout.ShowAt(e.OriginalSource as FrameworkElement ?? listView);
    }

    private void SeekToMarker(PodcastChapterMarkerItemViewModel item)
    {
        if (_mediaPlayer?.PlaybackSession is not { } session)
        {
            return;
        }

        session.Position = TimeSpan.FromSeconds(item.Seconds);
        _mediaPlayer.Play();
        SetStatusMessage($"Przejście do {item.TimeLabel}.");
        RestoreChapterMarkerSelection(item);
    }

    private async Task OpenRelatedLinkAsync(PodcastRelatedLinkItemViewModel item)
    {
        var launched = await _externalLinkLauncher.LaunchAsync(item.Url.AbsoluteUri);
        if (!launched)
        {
            ErrorBar.Message = "Nie udało się otworzyć odnośnika.";
            ErrorBar.IsOpen = true;
            ErrorBar.Visibility = Visibility.Visible;
            SetStatusMessage(ErrorBar.Message);
            return;
        }

        SetStatusMessage($"Otwarto odnośnik: {item.Title}.");
        RestoreRelatedLinkSelection(item);
    }

    private async Task CopyRelatedLinkAsync(PodcastRelatedLinkItemViewModel item)
    {
        var copied = await _clipboardService.SetTextAsync(item.Url.AbsoluteUri);
        if (!copied)
        {
            ErrorBar.Message = "Nie udało się skopiować odnośnika.";
            ErrorBar.IsOpen = true;
            ErrorBar.Visibility = Visibility.Visible;
            SetStatusMessage(ErrorBar.Message);
            return;
        }

        SetStatusMessage($"Skopiowano odnośnik: {item.Title}.");
        RestoreRelatedLinkSelection(item);
    }

    private async Task ShareRelatedLinkAsync(PodcastRelatedLinkItemViewModel item)
    {
        var shared = await _shareService.ShareLinkAsync(
            item.Title,
            _currentRequest?.Title,
            item.Url.AbsoluteUri
        );
        if (!shared)
        {
            ErrorBar.Message = "Nie udało się udostępnić odnośnika.";
            ErrorBar.IsOpen = true;
            ErrorBar.Visibility = Visibility.Visible;
            SetStatusMessage(ErrorBar.Message);
            return;
        }

        SetStatusMessage($"Otwarto systemowe udostępnianie dla: {item.Title}.");
        RestoreRelatedLinkSelection(item);
    }

    private void ToggleCommentDetails(CommentItemViewModel item)
    {
        var shouldExpand = !item.IsExpanded;
        foreach (var candidate in _comments)
        {
            candidate.IsExpanded = ReferenceEquals(candidate, item) && shouldExpand;
        }

        SetStatusMessage(
            shouldExpand
                ? $"Pokazano szczegóły komentarza: {item.AuthorName}."
                : $"Ukryto szczegóły komentarza: {item.AuthorName}."
        );
        RestoreCommentSelection(item);
    }

    private Border BuildCommentComposerPanel()
    {
        _isSynchronizingCommentComposer = true;
        try
        {
            var host = new StackPanel { Spacing = 10 };

            host.Children.Add(new TextBlock
            {
                Text = _commentComposerViewModel.FormHeadingText,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            host.Children.Add(new TextBlock { Text = "Pola oznaczone * są obowiązkowe." });

            if (_commentComposerViewModel.HasReplyTarget)
            {
                host.Children.Add(new TextBlock
                {
                    Text = _commentComposerViewModel.ReplyTargetText,
                    TextWrapping = TextWrapping.WrapWholeWords,
                });

                var cancelReplyButton = new Button
                {
                    Content = "Anuluj odpowiedź",
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                cancelReplyButton.Click += OnCancelCommentReplyClick;
                host.Children.Add(cancelReplyButton);
            }

            _commentAuthorNameTextBox = new TextBox
            {
                Header = "Imię *",
                Text = _commentComposerViewModel.AuthorName,
            };
            _commentAuthorNameTextBox.TextChanged += OnCommentAuthorNameTextChanged;
            host.Children.Add(_commentAuthorNameTextBox);

            _commentAuthorEmailTextBox = new TextBox
            {
                Header = "Adres e-mail *",
                Text = _commentComposerViewModel.AuthorEmail,
                InputScope = new InputScope
                {
                    Names = { new InputScopeName(InputScopeNameValue.EmailSmtpAddress) },
                },
            };
            _commentAuthorEmailTextBox.TextChanged += OnCommentAuthorEmailTextChanged;
            host.Children.Add(_commentAuthorEmailTextBox);

            _commentContentTextBox = new TextBox
            {
                Header = "Treść komentarza *",
                Text = _commentComposerViewModel.Content,
                AcceptsReturn = true,
                MinHeight = 120,
                TextWrapping = TextWrapping.Wrap,
            };
            _commentContentTextBox.TextChanged += OnCommentContentTextChanged;
            host.Children.Add(_commentContentTextBox);

            var submitButton = new Button
            {
                Content = _commentComposerViewModel.SubmitButtonText,
                IsEnabled = _commentComposerViewModel.CanSubmit,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            submitButton.Click += OnSubmitCommentClick;
            host.Children.Add(submitButton);

            return new Border
            {
                Padding = new Thickness(12),
                Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
                BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = host,
            };
        }
        finally
        {
            _isSynchronizingCommentComposer = false;
        }
    }

    private void OnCommentComposerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isCommentComposerVisible)
        {
            UpdateShowNotesUi(null);
        }
    }

    private void OnCommentAuthorNameTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSynchronizingCommentComposer && sender is TextBox textBox)
        {
            _commentComposerViewModel.AuthorName = textBox.Text;
        }
    }

    private void OnCommentAuthorEmailTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSynchronizingCommentComposer && sender is TextBox textBox)
        {
            _commentComposerViewModel.AuthorEmail = textBox.Text;
        }
    }

    private void OnCommentContentTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSynchronizingCommentComposer && sender is TextBox textBox)
        {
            _commentComposerViewModel.Content = textBox.Text;
        }
    }

    private void OnCancelCommentReplyClick(object sender, RoutedEventArgs e)
    {
        _commentComposerViewModel.CancelReply();
        UpdateShowNotesUi(null);
        FocusCommentComposerField(PodcastCommentFormField.Content);
    }

    private async void OnSubmitCommentClick(object sender, RoutedEventArgs e)
    {
        var result = await _commentComposerViewModel.SubmitAsync();
        SetStatusMessage(result.Message, announce: true, important: !result.Accepted);

        if (!result.Accepted)
        {
            FocusCommentComposerField(result.FocusTarget);
            return;
        }

        if (_currentRequest?.PodcastPostId is not int podcastPostId)
        {
            return;
        }

        _isCommentsVisible = true;
        await LoadShowNotesAsync(podcastPostId, CancellationToken.None);
        _isCommentsVisible = true;
        _isCommentComposerVisible = false;
        UpdateShowNotesUi(null);

        if (_comments.Length > 0)
        {
            FocusCommentsList();
            return;
        }

        DispatcherQueue.TryEnqueue(() => _addCommentButton?.Focus(FocusState.Programmatic));
    }

    private void FocusCommentComposerField(PodcastCommentFormField field)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (field)
            {
                case PodcastCommentFormField.AuthorName:
                    _commentAuthorNameTextBox?.Focus(FocusState.Programmatic);
                    break;
                case PodcastCommentFormField.AuthorEmail:
                    _commentAuthorEmailTextBox?.Focus(FocusState.Programmatic);
                    break;
                case PodcastCommentFormField.Content:
                    _commentContentTextBox?.Focus(FocusState.Programmatic);
                    break;
            }
        });
    }

    private async Task ToggleChapterMarkerFavoriteAsync(PodcastChapterMarkerItemViewModel item)
    {
        if (_currentRequest?.PodcastPostId is not int podcastPostId)
        {
            return;
        }

        try
        {
            var favoriteItem = CreateTopicFavoriteItem(item, podcastPostId);
            if (item.IsFavorite)
            {
                await _favoritesService.RemoveAsync(favoriteItem.Id);
                SetStatusMessage(
                    $"Usunięto temat z ulubionych: {item.Title}.",
                    announce: true,
                    important: true
                );
            }
            else
            {
                await _favoritesService.AddOrUpdateAsync(favoriteItem);
                SetStatusMessage(
                    $"Dodano temat do ulubionych: {item.Title}.",
                    announce: true,
                    important: true
                );
            }

            _chapterMarkers = _chapterMarkers
                .Select(candidate =>
                    candidate.Equals(item) ? item with { IsFavorite = !item.IsFavorite } : candidate
                )
                .ToArray();
            UpdateShowNotesUi(null);
            RestoreChapterMarkerSelection(item with { IsFavorite = !item.IsFavorite });
        }
        catch
        {
            ErrorBar.Message = "Nie udało się zaktualizować ulubionego tematu.";
            ErrorBar.IsOpen = true;
            ErrorBar.Visibility = Visibility.Visible;
            SetStatusMessage(ErrorBar.Message);
        }
    }

    private async Task ToggleRelatedLinkFavoriteAsync(PodcastRelatedLinkItemViewModel item)
    {
        if (_currentRequest?.PodcastPostId is not int podcastPostId)
        {
            return;
        }

        try
        {
            var favoriteItem = CreateRelatedLinkFavoriteItem(item, podcastPostId);
            if (item.IsFavorite)
            {
                await _favoritesService.RemoveAsync(favoriteItem.Id);
                SetStatusMessage(
                    $"Usunięto odnośnik z ulubionych: {item.Title}.",
                    announce: true,
                    important: true
                );
            }
            else
            {
                await _favoritesService.AddOrUpdateAsync(favoriteItem);
                SetStatusMessage(
                    $"Dodano odnośnik do ulubionych: {item.Title}.",
                    announce: true,
                    important: true
                );
            }

            _relatedLinks = _relatedLinks
                .Select(candidate =>
                    candidate.Equals(item) ? item with { IsFavorite = !item.IsFavorite } : candidate
                )
                .ToArray();
            UpdateShowNotesUi(null);
            RestoreRelatedLinkSelection(item with { IsFavorite = !item.IsFavorite });
        }
        catch
        {
            ErrorBar.Message = "Nie udało się zaktualizować ulubionego odnośnika.";
            ErrorBar.IsOpen = true;
            ErrorBar.Visibility = Visibility.Visible;
            SetStatusMessage(ErrorBar.Message);
        }
    }

    private async Task ToggleFocusedShowNotesFavoriteAsync()
    {
        if (TryGetFocusedChapterMarker(out var chapterMarker))
        {
            await ToggleChapterMarkerFavoriteAsync(chapterMarker);
            return;
        }

        if (TryGetFocusedRelatedLink(out var relatedLink))
        {
            await ToggleRelatedLinkFavoriteAsync(relatedLink);
        }
    }

    private void UpdateShowNotesUi(string? statusMessage)
    {
        ShowNotesPanel.Children.Clear();
        _chapterMarkersListView = null;
        _commentsListView = null;
        _relatedLinksListView = null;

        if (_isLoadingShowNotes)
        {
            ShowNotesPanel.Visibility = Visibility.Visible;
            ShowNotesPanel.Children.Add(new ProgressRing
            {
                IsActive = true,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            ShowNotesPanel.Visibility = Visibility.Visible;
            ShowNotesPanel.Children.Add(new TextBlock
            {
                Text = statusMessage,
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
            });
        }

        var canComment = _currentRequest?.PodcastPostId is int;

        if (_comments.Length == 0 && _chapterMarkers.Length == 0 && _relatedLinks.Length == 0 && !canComment)
        {
            if (!_isLoadingShowNotes && string.IsNullOrWhiteSpace(statusMessage))
            {
                ShowNotesPanel.Visibility = Visibility.Collapsed;
            }

            return;
        }

        ShowNotesPanel.Visibility = Visibility.Visible;

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        if (canComment)
        {
            var addCommentButton = new Button
            {
                Content = "Dodaj komentarz",
            };
            AutomationProperties.SetName(addCommentButton, "Dodaj komentarz");
            addCommentButton.Click += OnAddCommentClick;
            _addCommentButton = addCommentButton;
            actionsPanel.Children.Add(addCommentButton);
        }

        if (_comments.Length > 0)
        {
            ShowNotesPanel.Children.Add(new TextBlock
            {
                Text = _comments.Length == 1 ? "Komentarze: 1" : $"Komentarze: {_comments.Length}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });

            var button = new Button
            {
                Content = _isCommentsVisible ? "Ukryj komentarze" : "Pokaż komentarze",
            };
            AutomationProperties.SetName(
                button,
                _isCommentsVisible ? "Ukryj komentarze" : "Pokaż komentarze"
            );
            button.Click += OnToggleCommentsClick;
            actionsPanel.Children.Add(button);
        }

        if (_chapterMarkers.Length > 0)
        {
            var button = new Button
            {
                Content = _isChapterMarkersVisible ? "Ukryj znaczniki czasu" : "Pokaż znaczniki czasu",
            };
            AutomationProperties.SetName(
                button,
                _isChapterMarkersVisible ? "Ukryj znaczniki czasu" : "Pokaż znaczniki czasu"
            );
            button.Click += OnToggleChapterMarkersClick;
            actionsPanel.Children.Add(button);
        }

        if (_relatedLinks.Length > 0)
        {
            var button = new Button
            {
                Content = _isRelatedLinksVisible ? "Ukryj odnośniki" : "Pokaż odnośniki",
            };
            AutomationProperties.SetName(
                button,
                _isRelatedLinksVisible ? "Ukryj odnośniki" : "Pokaż odnośniki"
            );
            button.Click += OnToggleRelatedLinksClick;
            actionsPanel.Children.Add(button);
        }

        ShowNotesPanel.Children.Add(actionsPanel);

        if (_isCommentComposerVisible && canComment)
        {
            ShowNotesPanel.Children.Add(BuildCommentComposerPanel());
        }

        if (_isCommentsVisible && _comments.Length > 0)
        {
            ShowNotesPanel.Children.Add(new TextBlock
            {
                Text = "Komentarze",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            var listView = new ListView
            {
                ItemsSource = _comments,
                ItemTemplate = (DataTemplate)Resources["CommentItemTemplate"],
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.Single,
            };
            AutomationProperties.SetName(listView, "Komentarze podcastu");
            listView.ItemClick += OnCommentsListItemClick;
            listView.KeyDown += OnCommentsListKeyDown;
            listView.ContextRequested += OnCommentsListContextRequested;
            _commentsListView = listView;
            ShowNotesPanel.Children.Add(listView);
        }

        if (_isChapterMarkersVisible && _chapterMarkers.Length > 0)
        {
            ShowNotesPanel.Children.Add(new TextBlock
            {
                Text = "Znaczniki czasu",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            var listView = new ListView
            {
                ItemsSource = _chapterMarkers,
                ItemTemplate = (DataTemplate)Resources["ChapterMarkerItemTemplate"],
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.Single,
            };
            AutomationProperties.SetName(listView, "Znaczniki czasu");
            listView.ItemClick += OnChapterMarkersListItemClick;
            listView.KeyDown += OnChapterMarkersListKeyDown;
            listView.ContextRequested += OnChapterMarkersListContextRequested;
            _chapterMarkersListView = listView;
            ShowNotesPanel.Children.Add(listView);
        }

        if (_isRelatedLinksVisible && _relatedLinks.Length > 0)
        {
            ShowNotesPanel.Children.Add(new TextBlock
            {
                Text = "Odnośniki",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            var listView = new ListView
            {
                ItemsSource = _relatedLinks,
                ItemTemplate = (DataTemplate)Resources["RelatedLinkItemTemplate"],
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.Single,
            };
            AutomationProperties.SetName(listView, "Odnośniki");
            listView.ItemClick += OnRelatedLinksListItemClick;
            listView.KeyDown += OnRelatedLinksListKeyDown;
            listView.ContextRequested += OnRelatedLinksListContextRequested;
            _relatedLinksListView = listView;
            ShowNotesPanel.Children.Add(listView);
        }
    }

    private bool TryGetFocusedChapterMarker(out PodcastChapterMarkerItemViewModel item)
    {
        item = default!;

        if (
            _chapterMarkersListView?.SelectedItem is not PodcastChapterMarkerItemViewModel selectedItem
            || !IsFocusWithinList(_chapterMarkersListView)
        )
        {
            return false;
        }

        item = selectedItem;
        return true;
    }

    private bool TryGetFocusedRelatedLink(out PodcastRelatedLinkItemViewModel item)
    {
        item = default!;

        if (
            _relatedLinksListView?.SelectedItem is not PodcastRelatedLinkItemViewModel selectedItem
            || !IsFocusWithinList(_relatedLinksListView)
        )
        {
            return false;
        }

        item = selectedItem;
        return true;
    }

    private bool IsFocusWithinList(ListView listView)
    {
        var focusedElement = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (focusedElement is not null)
        {
            if (ReferenceEquals(focusedElement, listView))
            {
                return true;
            }

            focusedElement = VisualTreeHelper.GetParent(focusedElement);
        }

        return false;
    }

    private void RestoreChapterMarkerSelection(PodcastChapterMarkerItemViewModel item)
    {
        if (_chapterMarkersListView is null)
        {
            return;
        }

        var selectedItem = _chapterMarkers.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, item.Title, StringComparison.Ordinal)
            && Math.Abs(candidate.Seconds - item.Seconds) < 0.001d
        );
        if (selectedItem is null)
        {
            return;
        }

        _chapterMarkersListView.SelectedItem = selectedItem;
        _chapterMarkersListView.ScrollIntoView(selectedItem);
        _chapterMarkersListView.UpdateLayout();
        _chapterMarkersListView.Focus(FocusState.Programmatic);
    }

    private void RestoreCommentSelection(CommentItemViewModel item)
    {
        if (_commentsListView is null)
        {
            return;
        }

        var selectedItem = _comments.FirstOrDefault(candidate => candidate.Id == item.Id);
        if (selectedItem is null)
        {
            return;
        }

        _commentsListView.SelectedItem = selectedItem;
        _commentsListView.ScrollIntoView(selectedItem);
        _commentsListView.UpdateLayout();
        _commentsListView.Focus(FocusState.Programmatic);
    }

    private void FocusChapterMarkersList()
    {
        if (_chapterMarkersListView is null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            _chapterMarkersListView.UpdateLayout();
            if (_chapterMarkersListView.SelectedItem is null && _chapterMarkers.Length > 0)
            {
                _chapterMarkersListView.SelectedItem = _chapterMarkers[0];
                _chapterMarkersListView.ScrollIntoView(_chapterMarkers[0]);
            }

            _chapterMarkersListView.Focus(FocusState.Programmatic);
        });
    }

    private void FocusCommentsList()
    {
        if (_commentsListView is null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            _commentsListView.UpdateLayout();
            if (_commentsListView.SelectedItem is null && _comments.Length > 0)
            {
                _commentsListView.SelectedItem = _comments[0];
                _commentsListView.ScrollIntoView(_comments[0]);
            }

            _commentsListView.Focus(FocusState.Programmatic);
        });
    }

    private void RestoreRelatedLinkSelection(PodcastRelatedLinkItemViewModel item)
    {
        if (_relatedLinksListView is null)
        {
            return;
        }

        var selectedItem = _relatedLinks.FirstOrDefault(candidate =>
            Uri.Compare(candidate.Url, item.Url, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.Ordinal)
                == 0
        );
        if (selectedItem is null)
        {
            return;
        }

        _relatedLinksListView.SelectedItem = selectedItem;
        _relatedLinksListView.ScrollIntoView(selectedItem);
        _relatedLinksListView.UpdateLayout();
        _relatedLinksListView.Focus(FocusState.Programmatic);
    }

    private void FocusRelatedLinksList()
    {
        if (_relatedLinksListView is null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            _relatedLinksListView.UpdateLayout();
            if (_relatedLinksListView.SelectedItem is null && _relatedLinks.Length > 0)
            {
                _relatedLinksListView.SelectedItem = _relatedLinks[0];
                _relatedLinksListView.ScrollIntoView(_relatedLinks[0]);
            }

            _relatedLinksListView.Focus(FocusState.Programmatic);
        });
    }

    private void ResetShowNotesState()
    {
        CancelShowNotesLoad();
        _comments = [];
        _chapterMarkers = [];
        _relatedLinks = [];
        _addCommentButton = null;
        _commentsListView = null;
        _chapterMarkersListView = null;
        _relatedLinksListView = null;
        _commentAuthorNameTextBox = null;
        _commentAuthorEmailTextBox = null;
        _commentContentTextBox = null;
        _isLoadingShowNotes = false;
        _isCommentsVisible = false;
        _isCommentComposerVisible = false;
        _isChapterMarkersVisible = false;
        _isRelatedLinksVisible = false;
        ShowNotesPanel.Children.Clear();
        ShowNotesPanel.Visibility = Visibility.Collapsed;
    }

    private async void OnAddCommentClick(object sender, RoutedEventArgs e)
    {
        await ShowCommentComposerAsync();
    }

    private async Task ShowCommentComposerAsync(CommentItemViewModel? replyTarget = null)
    {
        if (_currentRequest?.PodcastPostId is not int podcastPostId)
        {
            return;
        }

        try
        {
            await _commentComposerViewModel.LoadIfNeededAsync();
            _isCommentComposerVisible = true;

            if (replyTarget is null)
            {
                _commentComposerViewModel.CancelReply();
                SetStatusMessage("Dodawanie nowego komentarza.", announce: true);
            }
            else
            {
                _commentComposerViewModel.BeginReply(replyTarget);
                SetStatusMessage(
                    $"Odpowiadasz na komentarz autora: {replyTarget.AuthorName}.",
                    announce: true
                );
            }

            UpdateShowNotesUi(null);
            FocusCommentComposerField(PodcastCommentFormField.AuthorName);
        }
        catch
        {
            ErrorBar.Message = "Nie udało się przygotować formularza komentarza.";
            ErrorBar.IsOpen = true;
            ErrorBar.Visibility = Visibility.Visible;
            SetStatusMessage(ErrorBar.Message, announce: true);
        }
    }

    private void CancelShowNotesLoad()
    {
        _showNotesLoadCts?.Cancel();
        _showNotesLoadCts?.Dispose();
        _showNotesLoadCts = null;
    }

    private async Task<PodcastChapterMarkerItemViewModel[]> LoadChapterMarkerFavoritesAsync(
        PodcastChapterMarkerItemViewModel[] items,
        int podcastPostId,
        CancellationToken cancellationToken
    )
    {
        var favoriteTasks = items.Select(async item =>
        {
            var favoriteId = FavoriteItem.CreateTopicId(podcastPostId, item.Title, item.Seconds);
            var isFavorite = await _favoritesService.IsFavoriteAsync(favoriteId, cancellationToken);
            return item with { IsFavorite = isFavorite };
        });

        return await Task.WhenAll(favoriteTasks);
    }

    private async Task<PodcastRelatedLinkItemViewModel[]> LoadRelatedLinkFavoritesAsync(
        PodcastRelatedLinkItemViewModel[] items,
        int podcastPostId,
        CancellationToken cancellationToken
    )
    {
        var favoriteTasks = items.Select(async item =>
        {
            var favoriteId = FavoriteItem.CreateLinkId(podcastPostId, item.Url.AbsoluteUri);
            var isFavorite = await _favoritesService.IsFavoriteAsync(favoriteId, cancellationToken);
            return item with { IsFavorite = isFavorite };
        });

        return await Task.WhenAll(favoriteTasks);
    }

    private async Task PersistCurrentPositionIfNeededAsync(MediaPlayer mediaPlayer)
    {
        if (_currentRequest is null || _currentRequest.IsLive || _hasPlaybackEnded)
        {
            return;
        }

        var positionSeconds = mediaPlayer.PlaybackSession.Position.TotalSeconds;
        if (positionSeconds <= 1d)
        {
            return;
        }

        await PersistResumePositionAsync(_currentRequest.SourceUrl, positionSeconds);
    }

    private async Task PersistResumePositionAsync(Uri sourceUrl, double positionSeconds)
    {
        try
        {
            await _playbackResumeService.SaveResumePositionAsync(sourceUrl, positionSeconds);
        }
        catch
        {
        }
    }

    private async Task ClearResumePositionAsync(Uri sourceUrl)
    {
        try
        {
            await _playbackResumeService.ClearResumePositionAsync(sourceUrl);
        }
        catch
        {
        }
    }

    private void DetachMediaPlayer(MediaPlayer mediaPlayer)
    {
        mediaPlayer.MediaFailed -= OnMediaFailed;
        mediaPlayer.MediaOpened -= OnMediaOpened;
        mediaPlayer.MediaEnded -= OnMediaEnded;
        mediaPlayer.PlaybackSession.PositionChanged -= OnPlaybackSessionPositionChanged;
        mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackSessionPlaybackStateChanged;
    }

    private static string GetHostLabel(Uri url)
    {
        if (!string.IsNullOrWhiteSpace(url.Host))
        {
            return url.Host;
        }

        return string.Equals(url.Scheme, "mailto", StringComparison.OrdinalIgnoreCase)
            ? "e-mail"
            : url.Scheme;
    }

    private FavoriteItem CreateTopicFavoriteItem(PodcastChapterMarkerItemViewModel item, int podcastPostId)
    {
        return new FavoriteItem
        {
            Id = FavoriteItem.CreateTopicId(podcastPostId, item.Title, item.Seconds),
            Kind = FavoriteKind.Topic,
            Source = ContentSource.Podcast,
            PostId = podcastPostId,
            Title = item.Title,
            Subtitle = _currentRequest?.Title ?? string.Empty,
            PublishedDate = _currentRequest?.Subtitle ?? string.Empty,
            ContextTitle = _currentRequest?.Title ?? string.Empty,
            ContextSubtitle = _currentRequest?.Subtitle ?? string.Empty,
            StartPositionSeconds = item.Seconds,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private FavoriteItem CreateRelatedLinkFavoriteItem(PodcastRelatedLinkItemViewModel item, int podcastPostId)
    {
        return new FavoriteItem
        {
            Id = FavoriteItem.CreateLinkId(podcastPostId, item.Url.AbsoluteUri),
            Kind = FavoriteKind.Link,
            Source = ContentSource.Podcast,
            PostId = podcastPostId,
            Title = item.Title,
            Subtitle = item.HostLabel,
            PublishedDate = _currentRequest?.Subtitle ?? string.Empty,
            Link = item.Url.AbsoluteUri,
            ContextTitle = _currentRequest?.Title ?? string.Empty,
            ContextSubtitle = _currentRequest?.Subtitle ?? string.Empty,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private string BuildShowNotesReadyMessage()
    {
        if (_comments.Length > 0)
        {
            return _comments.Length == 1
                ? "Dodatki do odcinka są gotowe. Komentarze: 1."
                : $"Dodatki do odcinka są gotowe. Komentarze: {_comments.Length}.";
        }

        return "Dodatki do odcinka są gotowe.";
    }

    private sealed record PlaybackRateOption(string Label, double Value);
}
