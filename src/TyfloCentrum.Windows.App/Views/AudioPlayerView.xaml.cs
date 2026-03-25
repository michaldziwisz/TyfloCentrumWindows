using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TyfloCentrum.Windows.App.Services;
using Windows.Devices.Enumeration;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.System;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class AudioPlayerView : UserControl
{
    private const double DefaultVolumePercent = 100d;
    private const double ResumePersistIntervalSeconds = 5d;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;
    private readonly IClipboardService _clipboardService;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFavoritesService _favoritesService;
    private readonly IPlaybackResumeService _playbackResumeService;
    private readonly IShareService _shareService;
    private readonly IWordPressCommentsService _wordPressCommentsService;
    private readonly PlaybackRateOption[] _playbackRates;
    private CancellationTokenSource? _showNotesLoadCts;
    private ChapterMarkerItem[] _chapterMarkers = [];
    private RelatedLinkItem[] _relatedLinks = [];
    private ListView? _chapterMarkersListView;
    private ListView? _relatedLinksListView;
    private MediaPlayer? _mediaPlayer;
    private AppSettingsSnapshot _currentSettings = AppSettingsSnapshot.Defaults;
    private bool _isChapterMarkersVisible;
    private bool _isLoadingShowNotes;
    private bool _isRelatedLinksVisible;
    private bool _hasPlaybackEnded;
    private bool _isRestoringResumePosition;
    private bool _isSynchronizingVolume;
    private double _lastPersistedResumeSeconds;
    private AudioPlaybackRequest? _currentRequest;
    private double? _pendingResumePositionSeconds;

    public AudioPlayerView(
        IAppSettingsService appSettingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService,
        IClipboardService clipboardService,
        IFavoritesService favoritesService,
        IPlaybackResumeService playbackResumeService,
        IShareService shareService,
        IWordPressCommentsService wordPressCommentsService,
        IExternalLinkLauncher externalLinkLauncher
    )
    {
        _appSettingsService = appSettingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
        _clipboardService = clipboardService;
        _favoritesService = favoritesService;
        _playbackResumeService = playbackResumeService;
        _shareService = shareService;
        _wordPressCommentsService = wordPressCommentsService;
        _externalLinkLauncher = externalLinkLauncher;
        _playbackRates = PlaybackRateCatalog
            .SupportedValues.Select(value => new PlaybackRateOption(PlaybackRateCatalog.FormatLabel(value), value))
            .ToArray();
        InitializeComponent();
        PlaybackRateComboBox.ItemsSource = _playbackRates;
        PlaybackRateComboBox.DisplayMemberPath = nameof(PlaybackRateOption.Label);
        PlaybackRateComboBox.SelectedItem = _playbackRates.First(option =>
            option.Value == PlaybackRateCatalog.DefaultValue
        );
        SetVolume(DefaultVolumePercent, announce: false);
    }

    public async Task InitializeAsync(
        AudioPlaybackRequest request,
        CancellationToken cancellationToken = default
    )
    {
        _currentRequest = null;
        _pendingResumePositionSeconds = null;
        _lastPersistedResumeSeconds = 0;
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
        ConfigureTransportControls(request);
        await ConfigureMediaPlayerAsync(request, cancellationToken);

        if (!request.IsLive && request.PodcastPostId is int podcastPostId)
        {
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
            _hasPlaybackEnded = false;
            _isRestoringResumePosition = false;
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
        _hasPlaybackEnded = false;
        _isRestoringResumePosition = false;
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
        var transportControls = PlayerElement.TransportControls;
        transportControls.IsNextTrackButtonVisible = false;
        transportControls.IsPreviousTrackButtonVisible = false;
        transportControls.IsRepeatButtonVisible = false;
        transportControls.IsPlaybackRateButtonVisible = false;
        transportControls.IsPlaybackRateEnabled = false;
        transportControls.IsSeekBarVisible = request.CanSeek;
        transportControls.IsSeekEnabled = request.CanSeek;
        transportControls.IsFastForwardButtonVisible = request.CanSeek;
        transportControls.IsFastForwardEnabled = request.CanSeek;
        transportControls.IsFastRewindButtonVisible = request.CanSeek;
        transportControls.IsFastRewindEnabled = request.CanSeek;
        transportControls.IsSkipBackwardButtonVisible = false;
        transportControls.IsSkipForwardButtonVisible = false;
        transportControls.IsZoomButtonVisible = false;
        transportControls.IsZoomEnabled = false;
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!KeyboardShortcutHelper.IsControlPressed())
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Space:
                e.Handled = true;
                TogglePlayback();
                break;
            case VirtualKey.Left when _currentRequest?.CanSeek == true:
                e.Handled = true;
                SeekBy(TimeSpan.FromSeconds(-30), "Przewinięto wstecz o 30 sekund.");
                break;
            case VirtualKey.Right when _currentRequest?.CanSeek == true:
                e.Handled = true;
                SeekBy(TimeSpan.FromSeconds(30), "Przewinięto do przodu o 30 sekund.");
                break;
            case VirtualKey.Up:
                e.Handled = true;
                AdjustPlaybackRate(1);
                break;
            case VirtualKey.Down:
                e.Handled = true;
                AdjustPlaybackRate(-1);
                break;
            case VirtualKey.D:
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

    private void OnVolumeSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
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
            SetStatusMessage(
                _currentRequest?.IsLive == true
                    ? "Wstrzymano transmisję na żywo."
                    : "Wstrzymano odtwarzanie."
            );
            return;
        }

        _mediaPlayer.Play();
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
            SetStatusMessage(ErrorBar.Message);
        });
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        if (_pendingResumePositionSeconds is not double resumePositionSeconds || resumePositionSeconds <= 1d)
        {
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
                return;
            }

            _isRestoringResumePosition = true;
            session.Position = TimeSpan.FromSeconds(targetSeconds);
            _lastPersistedResumeSeconds = targetSeconds;
            _pendingResumePositionSeconds = null;
            _isRestoringResumePosition = false;
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
        _ = PersistResumePositionAsync(_currentRequest.SourceUrl, positionSeconds);
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

    private async Task LoadShowNotesAsync(int podcastPostId, CancellationToken cancellationToken)
    {
        _isLoadingShowNotes = true;
        UpdateShowNotesUi("Ładowanie znaczników czasu i odnośników…");

        try
        {
            var comments = await _wordPressCommentsService.GetCommentsAsync(
                podcastPostId,
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = ShowNotesParser.Parse(comments);
            var chapterMarkers = parsed.Markers
                .Select(marker => new ChapterMarkerItem(
                    marker.Title,
                    marker.Seconds,
                    FormatTime(marker.Seconds)
                ))
                .ToArray();
            var relatedLinks = parsed.Links
                .Select(link => new RelatedLinkItem(
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

            _chapterMarkers = chapterMarkers;
            _relatedLinks = relatedLinks;

            _isChapterMarkersVisible = false;
            _isRelatedLinksVisible = false;
            _isLoadingShowNotes = false;

            UpdateShowNotesUi(
                _chapterMarkers.Length == 0 && _relatedLinks.Length == 0
                    ? null
                    : "Dodatki do odcinka są gotowe."
            );
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _chapterMarkers = [];
            _relatedLinks = [];
            _isLoadingShowNotes = false;
            UpdateShowNotesUi("Nie udało się wczytać dodatków do odcinka.");
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
            ? "Skróty: Ctrl+spacja odtwarzaj lub pauzuj, Ctrl+strzałka w lewo i prawo przewijają o 30 sekund, Ctrl+strzałka w górę i dół zmieniają prędkość, Ctrl+D przełącza ulubione dla zaznaczonego dodatku odcinka, Ctrl+U udostępnia zaznaczony odnośnik."
            : "Skróty: Ctrl+spacja odtwarzaj lub pauzuj, Ctrl+D przełącza ulubione dla zaznaczonego dodatku odcinka, Ctrl+U udostępnia zaznaczony odnośnik.";
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
            _isChapterMarkersVisible = false;
        }

        UpdateShowNotesUi("Widok odnośników został zaktualizowany.");

        if (_isRelatedLinksVisible)
        {
            FocusRelatedLinksList();
        }
    }

    private void OnChapterMarkersListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChapterMarkerItem item)
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

        if (sender is ListView { SelectedItem: ChapterMarkerItem item })
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
            ItemContextResolver.Resolve<ChapterMarkerItem>(e.OriginalSource)
            ?? listView.SelectedItem as ChapterMarkerItem;
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
        if (e.ClickedItem is RelatedLinkItem item)
        {
            await OpenRelatedLinkAsync(item);
        }
    }

    private async void OnRelatedLinksListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (
            KeyboardShortcutHelper.IsControlPressed()
            && sender is ListView { SelectedItem: RelatedLinkItem selectedItem }
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

        if (sender is ListView { SelectedItem: RelatedLinkItem item })
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
            ItemContextResolver.Resolve<RelatedLinkItem>(e.OriginalSource)
            ?? listView.SelectedItem as RelatedLinkItem;
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

    private void SeekToMarker(ChapterMarkerItem item)
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

    private async Task OpenRelatedLinkAsync(RelatedLinkItem item)
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

    private async Task CopyRelatedLinkAsync(RelatedLinkItem item)
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

    private async Task ShareRelatedLinkAsync(RelatedLinkItem item)
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

    private async Task ToggleChapterMarkerFavoriteAsync(ChapterMarkerItem item)
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

    private async Task ToggleRelatedLinkFavoriteAsync(RelatedLinkItem item)
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

        if (_chapterMarkers.Length == 0 && _relatedLinks.Length == 0)
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
            AutomationProperties.SetName(listView, "Lista znaczników czasu");
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
            AutomationProperties.SetName(listView, "Lista odnośników");
            listView.ItemClick += OnRelatedLinksListItemClick;
            listView.KeyDown += OnRelatedLinksListKeyDown;
            listView.ContextRequested += OnRelatedLinksListContextRequested;
            _relatedLinksListView = listView;
            ShowNotesPanel.Children.Add(listView);
        }
    }

    private bool TryGetFocusedChapterMarker(out ChapterMarkerItem item)
    {
        item = default!;

        if (
            _chapterMarkersListView?.SelectedItem is not ChapterMarkerItem selectedItem
            || !IsFocusWithinList(_chapterMarkersListView)
        )
        {
            return false;
        }

        item = selectedItem;
        return true;
    }

    private bool TryGetFocusedRelatedLink(out RelatedLinkItem item)
    {
        item = default!;

        if (
            _relatedLinksListView?.SelectedItem is not RelatedLinkItem selectedItem
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

    private void RestoreChapterMarkerSelection(ChapterMarkerItem item)
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

    private void RestoreRelatedLinkSelection(RelatedLinkItem item)
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
        _chapterMarkers = [];
        _relatedLinks = [];
        _chapterMarkersListView = null;
        _relatedLinksListView = null;
        _isLoadingShowNotes = false;
        _isChapterMarkersVisible = false;
        _isRelatedLinksVisible = false;
        ShowNotesPanel.Children.Clear();
        ShowNotesPanel.Visibility = Visibility.Collapsed;
    }

    private void CancelShowNotesLoad()
    {
        _showNotesLoadCts?.Cancel();
        _showNotesLoadCts?.Dispose();
        _showNotesLoadCts = null;
    }

    private async Task<ChapterMarkerItem[]> LoadChapterMarkerFavoritesAsync(
        ChapterMarkerItem[] items,
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

    private async Task<RelatedLinkItem[]> LoadRelatedLinkFavoritesAsync(
        RelatedLinkItem[] items,
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

    private FavoriteItem CreateTopicFavoriteItem(ChapterMarkerItem item, int podcastPostId)
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

    private FavoriteItem CreateRelatedLinkFavoriteItem(RelatedLinkItem item, int podcastPostId)
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

    private sealed record PlaybackRateOption(string Label, double Value);

    private sealed record ChapterMarkerItem(
        string Title,
        double Seconds,
        string TimeLabel,
        bool IsFavorite = false
    )
    {
        public string AccessibleLabel => $"{Title}. {TimeLabel}.";

        public string FavoriteMenuLabel =>
            $"{(IsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych")} (Ctrl+D): temat {Title}";

        public override string ToString() => AccessibleLabel;
    }

    private sealed record RelatedLinkItem(
        string Title,
        Uri Url,
        string HostLabel,
        bool IsFavorite = false
    )
    {
        public string AccessibleLabel =>
            string.IsNullOrWhiteSpace(HostLabel) ? Title : $"{Title}. {HostLabel}.";

        public string OpenMenuLabel => $"Otwórz odnośnik: {Title}";

        public string CopyMenuLabel => $"Kopiuj odnośnik: {Title}";

        public string ShareMenuLabel => $"Udostępnij odnośnik (Ctrl+U): {Title}";

        public string FavoriteMenuLabel =>
            $"{(IsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych")} (Ctrl+D): odnośnik {Title}";

        public override string ToString() => AccessibleLabel;
    }
}
