using Microsoft.UI.Dispatching;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Tyflocentrum.Windows.Domain.Audio;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.App.Services;

public sealed class WindowsVoiceMessageRecorder : IVoiceMessageRecorder
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(20);

    private readonly IAppSettingsService _appSettingsService;
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly List<StorageFile> _segmentFiles = [];

    private MediaCapture? _mediaCapture;
    private StorageFile? _recordedFile;
    private StorageFile? _activeSegmentFile;
    private PeriodicTimer? _elapsedTimer;
    private CancellationTokenSource? _elapsedLoopCts;
    private DateTimeOffset _recordingStartedAt;
    private TimeSpan _recordingElapsedBase;
    private bool _currentRecordingIsAppend;
    private bool _hasPrimedMicrophoneConsent;
    private bool _disposed;

    public WindowsVoiceMessageRecorder(
        IAppSettingsService appSettingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService
    )
    {
        _appSettingsService = appSettingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        MediaDevice.DefaultAudioCaptureDeviceChanged += OnDefaultAudioCaptureDeviceChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<VoiceMessageRecorderNotificationEventArgs>? NotificationRaised;

    public VoiceMessageRecorderState State { get; private set; } = VoiceMessageRecorderState.Idle;

    public TimeSpan Elapsed { get; private set; }

    public int SegmentCount => _segmentFiles.Count;

    public int RecordedDurationMs { get; private set; }

    public string? RecordedFilePath { get; private set; }

    public bool HasRecording =>
        SegmentCount > 0
        && !string.IsNullOrWhiteSpace(RecordedFilePath)
        && State is
            VoiceMessageRecorderState.Recording
            or VoiceMessageRecorderState.Preparing
            or VoiceMessageRecorderState.Recorded
            or VoiceMessageRecorderState.PlayingPreview;

    public async Task<VoiceMessageOperationResult> EnsureMicrophoneAccessAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            return await EnsureMicrophoneConsentAsync(cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task<VoiceMessageOperationResult> StartRecordingAsync(
        CancellationToken cancellationToken = default
    )
    {
        return StartRecordingCoreAsync(append: false, cancellationToken);
    }

    public Task<VoiceMessageOperationResult> StartAppendingAsync(
        CancellationToken cancellationToken = default
    )
    {
        return StartRecordingCoreAsync(append: true, cancellationToken);
    }

    public async Task<VoiceMessageOperationResult> StopRecordingAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (
                State != VoiceMessageRecorderState.Recording
                || _mediaCapture is null
                || _activeSegmentFile is null
            )
            {
                return new VoiceMessageOperationResult(false, "Nagrywanie nie jest aktywne.");
            }

            var wasAppend = _currentRecordingIsAppend;
            var previousOutputFile = _recordedFile;
            var previousDuration = TimeSpan.FromMilliseconds(RecordedDurationMs);
            var segmentFallbackDuration = DateTimeOffset.UtcNow - _recordingStartedAt;
            var segmentFile = _activeSegmentFile;

            _activeSegmentFile = null;
            _currentRecordingIsAppend = false;
            StopElapsedLoop();

            try
            {
                await _mediaCapture.StopRecordAsync();
            }
            catch
            {
                CleanupMediaCapture();
                await SafeDeleteFileAsync(segmentFile);

                if (wasAppend && HasRecording)
                {
                    SetElapsed(previousDuration);
                    SetState(VoiceMessageRecorderState.Recorded);
                }
                else
                {
                    await DeleteAllRecordingCoreAsync();
                    SetState(VoiceMessageRecorderState.Idle);
                }

                return new VoiceMessageOperationResult(false, "Nie udało się zakończyć nagrywania.");
            }

            CleanupMediaCapture();
            SetState(VoiceMessageRecorderState.Preparing);

            var segmentInfo = await TryReadRecordingInfoAsync(
                segmentFile,
                segmentFallbackDuration,
                cancellationToken
            );

            if (!segmentInfo.Success)
            {
                await SafeDeleteFileAsync(segmentFile);

                if (wasAppend && HasRecording)
                {
                    SetElapsed(previousDuration);
                    SetState(VoiceMessageRecorderState.Recorded);
                }
                else
                {
                    await DeleteAllRecordingCoreAsync();
                    SetState(VoiceMessageRecorderState.Idle);
                }

                return new VoiceMessageOperationResult(false, segmentInfo.ErrorMessage);
            }

            if (!wasAppend)
            {
                SetSegmentFiles([segmentFile]);
                var outputResult = await PrepareOutputFileAsync(
                    [segmentFile],
                    segmentInfo.Duration,
                    cancellationToken
                );

                if (!outputResult.Success || outputResult.File is null)
                {
                    await SafeDeleteFileAsync(segmentFile);
                    await DeleteAllRecordingCoreAsync();
                    SetState(VoiceMessageRecorderState.Idle);
                    return new VoiceMessageOperationResult(
                        false,
                        outputResult.ErrorMessage ?? "Nie udało się przygotować nagrania."
                    );
                }

                _recordedFile = outputResult.File;
                SetElapsed(outputResult.Duration);
                SetRecordedMetadata(outputResult.File.Path, outputResult.DurationMs);
                SetState(VoiceMessageRecorderState.Recorded);
                return new VoiceMessageOperationResult(true, null);
            }

            var updatedSegments = _segmentFiles.Concat([segmentFile]).ToArray();
            var mergeResult = await PrepareOutputFileAsync(
                updatedSegments,
                previousDuration + segmentInfo.Duration,
                cancellationToken
            );

            if (!mergeResult.Success || mergeResult.File is null)
            {
                await SafeDeleteFileAsync(segmentFile);
                SetElapsed(previousDuration);
                SetState(VoiceMessageRecorderState.Recorded);
                return new VoiceMessageOperationResult(
                    false,
                    mergeResult.ErrorMessage ?? "Nie udało się połączyć fragmentów głosówki."
                );
            }

            SetSegmentFiles(updatedSegments);
            _recordedFile = mergeResult.File;
            SetElapsed(mergeResult.Duration);
            SetRecordedMetadata(mergeResult.File.Path, mergeResult.DurationMs);
            SetState(VoiceMessageRecorderState.Recorded);

            if (previousOutputFile is not null && previousOutputFile.Path != mergeResult.File.Path)
            {
                await SafeDeleteFileAsync(previousOutputFile);
            }

            return new VoiceMessageOperationResult(true, null);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<VoiceMessageOperationResult> TogglePreviewAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(RecordedFilePath))
            {
                return new VoiceMessageOperationResult(false, "Najpierw nagraj głosówkę.");
            }

            if (State == VoiceMessageRecorderState.Recording)
            {
                return new VoiceMessageOperationResult(
                    false,
                    "Najpierw zatrzymaj nagrywanie, aby odsłuchać głosówkę."
                );
            }

            if (State == VoiceMessageRecorderState.Preparing)
            {
                return new VoiceMessageOperationResult(
                    false,
                    "Nagranie jest jeszcze przygotowywane. Poczekaj chwilę."
                );
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (State == VoiceMessageRecorderState.PlayingPreview)
            {
                SetState(VoiceMessageRecorderState.Recorded);
                return new VoiceMessageOperationResult(true, null);
            }

            var previewFile = await ResolvePreviewFileAsync(cancellationToken);
            if (previewFile is null)
            {
                SetState(VoiceMessageRecorderState.Recorded);
                return new VoiceMessageOperationResult(
                    false,
                    "Nie znaleziono gotowego pliku nagrania do odsłuchu."
                );
            }

            SetState(VoiceMessageRecorderState.PlayingPreview);
            return new VoiceMessageOperationResult(true, null);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task StopPreviewAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            StopPreviewStateCore();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task DeleteRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (State == VoiceMessageRecorderState.Recording && _mediaCapture is not null)
            {
                try
                {
                    StopElapsedLoop();
                    await _mediaCapture.StopRecordAsync();
                }
                catch
                {
                    // Best effort cleanup only.
                }

                CleanupMediaCapture();
            }

            StopPreviewStateCore();
            await DeleteAllRecordingCoreAsync();
            SetState(VoiceMessageRecorderState.Idle);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        MediaDevice.DefaultAudioCaptureDeviceChanged -= OnDefaultAudioCaptureDeviceChanged;
        StopElapsedLoop();
        CleanupMediaCapture();
        _operationGate.Dispose();
    }

    private async Task<VoiceMessageOperationResult> StartRecordingCoreAsync(
        bool append,
        CancellationToken cancellationToken
    )
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (State == VoiceMessageRecorderState.Recording)
            {
                return new VoiceMessageOperationResult(false, "Nagrywanie już trwa.");
            }

            if (State == VoiceMessageRecorderState.Preparing)
            {
                return new VoiceMessageOperationResult(
                    false,
                    "Poczekaj, aż aplikacja przygotuje bieżące nagranie."
                );
            }

            if (append && !HasRecording)
            {
                return new VoiceMessageOperationResult(false, "Najpierw nagraj pierwszy fragment.");
            }

            StopPreviewStateCore();

            if (!append)
            {
                await DeleteAllRecordingCoreAsync();
            }

            var consentResult = await EnsureMicrophoneConsentAsync(cancellationToken);
            if (!consentResult.Success)
            {
                return consentResult;
            }

            MediaCapture? capture = null;

            try
            {
                var settingsSnapshot = (await _appSettingsService.GetAsync(cancellationToken))
                    .Normalize();
                var preferredInputDeviceId = settingsSnapshot.PreferredInputDeviceId;

                if (!string.IsNullOrWhiteSpace(preferredInputDeviceId))
                {
                    var inputDevices = await _audioDeviceCatalogService.GetInputDevicesAsync(
                        cancellationToken
                    );
                    var deviceExists = inputDevices.Any(device =>
                        string.Equals(device.Id, preferredInputDeviceId, StringComparison.Ordinal)
                    );

                    if (!deviceExists)
                    {
                        return new VoiceMessageOperationResult(
                            false,
                            "Wybrane urządzenie wejściowe nie jest dostępne. Otwórz Ustawienia i wybierz inne albo wróć do domyślnego urządzenia systemowego."
                        );
                    }
                }

                capture = await CreateInitializedCaptureAsync(preferredInputDeviceId, cancellationToken);
                if (capture is null)
                {
                    if (!string.IsNullOrWhiteSpace(preferredInputDeviceId))
                    {
                        return new VoiceMessageOperationResult(
                            false,
                            "Nie udało się uruchomić wybranego urządzenia wejściowego. Sprawdź Ustawienia aplikacji albo wróć do domyślnego urządzenia systemowego."
                        );
                    }

                    return new VoiceMessageOperationResult(
                        false,
                        "Nie udało się uruchomić nagrywania z użyciem mikrofonu."
                    );
                }

                var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                    $"voice-segment-{Guid.NewGuid():N}.wav",
                    CreationCollisionOption.GenerateUniqueName
                );

                await capture.StartRecordToStorageFileAsync(CreateSegmentEncodingProfile(), file);

                _mediaCapture = capture;
                _activeSegmentFile = file;
                _currentRecordingIsAppend = append;
                _recordingStartedAt = DateTimeOffset.UtcNow;
                _recordingElapsedBase = append
                    ? TimeSpan.FromMilliseconds(RecordedDurationMs)
                    : TimeSpan.Zero;

                SetElapsed(_recordingElapsedBase);
                SetState(VoiceMessageRecorderState.Recording);
                StartElapsedLoop();
                return new VoiceMessageOperationResult(true, null);
            }
            catch (UnauthorizedAccessException)
            {
                if (capture is not null)
                {
                    DisposeCapture(capture);
                }

                return new VoiceMessageOperationResult(
                    false,
                    "Brak dostępu do mikrofonu. Przy pierwszej próbie Windows powinien pokazać systemowe pytanie o zgodę. Jeśli dostęp został już wcześniej odrzucony, włącz mikrofon dla aplikacji w ustawieniach prywatności Windows."
                );
            }
            catch
            {
                if (capture is not null)
                {
                    DisposeCapture(capture);
                }

                var settingsSnapshot = (await _appSettingsService.GetAsync(cancellationToken))
                    .Normalize();
                if (!string.IsNullOrWhiteSpace(settingsSnapshot.PreferredInputDeviceId))
                {
                    return new VoiceMessageOperationResult(
                        false,
                        "Nie udało się uruchomić wybranego urządzenia wejściowego. Sprawdź Ustawienia aplikacji albo wróć do domyślnego urządzenia systemowego."
                    );
                }

                return new VoiceMessageOperationResult(
                    false,
                    "Nie udało się uruchomić nagrywania z użyciem mikrofonu."
                );
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<VoiceMessageOperationResult> EnsureMicrophoneConsentAsync(
        CancellationToken cancellationToken
    )
    {
        if (_hasPrimedMicrophoneConsent)
        {
            return new VoiceMessageOperationResult(true, null);
        }

        try
        {
            var accessStatus = await RunOnUiThreadAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var capability = AppCapability.Create("microphone");
                var currentStatus = capability.CheckAccess();
                return currentStatus == AppCapabilityAccessStatus.Allowed
                    ? currentStatus
                    : await capability.RequestAccessAsync();
            });

            if (accessStatus == AppCapabilityAccessStatus.Allowed)
            {
                _hasPrimedMicrophoneConsent = true;
                return new VoiceMessageOperationResult(true, null);
            }

            return new VoiceMessageOperationResult(
                false,
                accessStatus switch
                {
                    AppCapabilityAccessStatus.DeniedByUser =>
                        "Brak dostępu do mikrofonu. Zezwól na użycie mikrofonu w systemowym oknie zgody Windows albo w ustawieniach prywatności, jeśli zgoda została już wcześniej odrzucona.",
                    AppCapabilityAccessStatus.DeniedBySystem =>
                        "System Windows blokuje dostęp do mikrofonu dla tej aplikacji. Włącz mikrofon w ustawieniach prywatności Windows.",
                    AppCapabilityAccessStatus.NotDeclaredByApp =>
                        "Aplikacja nie ma zadeklarowanej możliwości użycia mikrofonu.",
                    _ =>
                        "Windows wymaga zgody na użycie mikrofonu. Potwierdź systemowe pytanie o dostęp i spróbuj ponownie.",
                }
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new VoiceMessageOperationResult(
                false,
                "Nie udało się sprawdzić zgody na użycie mikrofonu. Spróbuj ponownie."
            );
        }
    }

    private static MediaEncodingProfile CreateSegmentEncodingProfile()
    {
        return MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
    }

    private static MediaEncodingProfile CreateOutputEncodingProfile()
    {
        return MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Auto);
    }

    private async Task<MediaCapture?> CreateInitializedCaptureAsync(
        string? preferredInputDeviceId,
        CancellationToken cancellationToken
    )
    {
        var rawAttempt = await TryInitializeCaptureAsync(
            preferredInputDeviceId,
            AudioProcessing.Raw,
            cancellationToken
        );
        if (rawAttempt.Success)
        {
            return rawAttempt.Capture;
        }

        if (rawAttempt.IsUnauthorized)
        {
            throw new UnauthorizedAccessException();
        }

        var fallbackAttempt = await TryInitializeCaptureAsync(
            preferredInputDeviceId,
            AudioProcessing.Default,
            cancellationToken
        );
        if (fallbackAttempt.IsUnauthorized)
        {
            throw new UnauthorizedAccessException();
        }

        return fallbackAttempt.Success ? fallbackAttempt.Capture : null;
    }

    private async Task<(bool Success, bool IsUnauthorized, MediaCapture? Capture)> TryInitializeCaptureAsync(
        string? preferredInputDeviceId,
        AudioProcessing audioProcessing,
        CancellationToken cancellationToken
    )
    {
        var capture = new MediaCapture();
        capture.Failed += OnMediaCaptureFailed;
        capture.RecordLimitationExceeded += OnRecordLimitationExceeded;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio,
                AudioProcessing = audioProcessing,
                MediaCategory = MediaCategory.Media,
                AudioDeviceId = preferredInputDeviceId,
            };

            await capture.InitializeAsync(settings);
            return (true, false, capture);
        }
        catch (UnauthorizedAccessException)
        {
            DisposeCapture(capture);
            return (false, true, null);
        }
        catch
        {
            DisposeCapture(capture);
            return (false, false, null);
        }
    }

    private void OnMediaCaptureFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
    {
        RunOnUiThread(() =>
        {
            _ = HandleRecordingFailureAsync(
                BuildCaptureFailureMessage(errorEventArgs),
                raiseNotification: true
            );
        });
    }

    private void OnRecordLimitationExceeded(MediaCapture sender)
    {
        _ = StopRecordingAsync();
    }

    private void OnDefaultAudioCaptureDeviceChanged(
        object sender,
        DefaultAudioCaptureDeviceChangedEventArgs args
    )
    {
        if (!string.IsNullOrWhiteSpace(args.Id))
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (State != VoiceMessageRecorderState.Recording)
            {
                return;
            }

            _ = HandleRecordingFailureAsync(
                "Mikrofon przestał być dostępny. Nagrywanie zostało zatrzymane.",
                raiseNotification: true
            );
        });
    }

    private void StartElapsedLoop()
    {
        StopElapsedLoop();

        _elapsedLoopCts = new CancellationTokenSource();
        _elapsedTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        _ = Task.Run(async () =>
        {
            try
            {
                while (
                    _elapsedTimer is not null
                    && await _elapsedTimer.WaitForNextTickAsync(_elapsedLoopCts.Token)
                )
                {
                    var elapsed = _recordingElapsedBase + (DateTimeOffset.UtcNow - _recordingStartedAt);
                    RunOnUiThread(() => SetElapsed(elapsed));

                    if (elapsed >= MaxDuration)
                    {
                        _ = StopRecordingAsync();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void StopElapsedLoop()
    {
        _elapsedLoopCts?.Cancel();
        _elapsedLoopCts?.Dispose();
        _elapsedLoopCts = null;
        _elapsedTimer?.Dispose();
        _elapsedTimer = null;
    }

    private void CleanupMediaCapture()
    {
        if (_mediaCapture is null)
        {
            return;
        }

        DisposeCapture(_mediaCapture);
        _mediaCapture = null;
    }

    private void DisposeCapture(MediaCapture capture)
    {
        capture.Failed -= OnMediaCaptureFailed;
        capture.RecordLimitationExceeded -= OnRecordLimitationExceeded;
        capture.Dispose();
    }

    private void StopPreviewStateCore()
    {
        if (State == VoiceMessageRecorderState.PlayingPreview)
        {
            SetState(_recordedFile is null ? VoiceMessageRecorderState.Idle : VoiceMessageRecorderState.Recorded);
        }
    }

    private async Task HandleRecordingFailureAsync(string message, bool raiseNotification)
    {
        await _operationGate.WaitAsync();
        try
        {
            if (State != VoiceMessageRecorderState.Recording && _activeSegmentFile is null)
            {
                return;
            }

            StopElapsedLoop();
            CleanupMediaCapture();

            if (_activeSegmentFile is not null)
            {
                await SafeDeleteFileAsync(_activeSegmentFile);
                _activeSegmentFile = null;
            }

            _currentRecordingIsAppend = false;

            if (HasRecording)
            {
                SetElapsed(TimeSpan.FromMilliseconds(RecordedDurationMs));
                SetState(VoiceMessageRecorderState.Recorded);
            }
            else
            {
                await DeleteAllRecordingCoreAsync();
                SetState(VoiceMessageRecorderState.Idle);
            }

            if (raiseNotification)
            {
                RaiseNotification(message, isError: true);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task DeleteAllRecordingCoreAsync()
    {
        var filesToDelete = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var segmentFile in _segmentFiles)
        {
            filesToDelete[segmentFile.Path] = segmentFile;
        }

        if (_recordedFile is not null)
        {
            filesToDelete[_recordedFile.Path] = _recordedFile;
        }

        if (_activeSegmentFile is not null)
        {
            filesToDelete[_activeSegmentFile.Path] = _activeSegmentFile;
        }

        foreach (var file in filesToDelete.Values)
        {
            await SafeDeleteFileAsync(file);
        }

        _recordedFile = null;
        _activeSegmentFile = null;
        _currentRecordingIsAppend = false;
        _recordingElapsedBase = TimeSpan.Zero;
        SetSegmentFiles([]);
        SetElapsed(TimeSpan.Zero);
        SetRecordedMetadata(null, 0);
    }

    private static async Task SafeDeleteFileAsync(StorageFile? file)
    {
        if (file is null)
        {
            return;
        }

        try
        {
            await file.DeleteAsync();
        }
        catch
        {
            // Best effort only. Temporary files may already be gone.
        }
    }

    private async Task<(bool Success, TimeSpan Duration, int DurationMs, string? ErrorMessage)>
        TryReadRecordingInfoAsync(
            StorageFile file,
            TimeSpan fallbackDuration,
            CancellationToken cancellationToken
        )
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var basicProperties = await file.GetBasicPropertiesAsync();
            if (basicProperties.Size <= 0)
            {
                return (false, TimeSpan.Zero, 0, "Nagranie jest puste.");
            }

            var musicProperties = await file.Properties.GetMusicPropertiesAsync();
            var duration = musicProperties.Duration > TimeSpan.Zero
                ? musicProperties.Duration
                : fallbackDuration;
            var durationMs = Math.Max(0, (int)Math.Round(duration.TotalMilliseconds));

            if (durationMs <= 0)
            {
                return (false, TimeSpan.Zero, 0, "Nie udało się odczytać długości nagrania.");
            }

            return (true, duration, durationMs, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return (false, TimeSpan.Zero, 0, "Nie udało się przygotować nagrania.");
        }
    }

    private async Task<StorageFile?> ResolvePreviewFileAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RecordedFilePath))
        {
            return null;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file =
                _recordedFile is not null
                && string.Equals(_recordedFile.Path, RecordedFilePath, StringComparison.OrdinalIgnoreCase)
                    ? _recordedFile
                    : await StorageFile.GetFileFromPathAsync(RecordedFilePath);
            var basicProperties = await file.GetBasicPropertiesAsync();
            if (basicProperties.Size <= 0)
            {
                return null;
            }

            _recordedFile = file;
            return file;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<(bool Success, StorageFile? File, TimeSpan Duration, int DurationMs, string? ErrorMessage)>
        PrepareOutputFileAsync(
            IReadOnlyList<StorageFile> segmentFiles,
            TimeSpan fallbackDuration,
            CancellationToken cancellationToken
        )
    {
        StorageFile? mergedWavFile = null;
        StorageFile? processedWavFile = null;
        StorageFile? outputFile = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            StorageFile sourceFile;
            if (segmentFiles.Count == 1)
            {
                sourceFile = segmentFiles[0];
            }
            else
            {
                mergedWavFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                    $"voice-merged-{Guid.NewGuid():N}.wav",
                    CreationCollisionOption.GenerateUniqueName
                );

                await Task.Run(
                    () =>
                        WavFileConcatenator.Concatenate(
                            segmentFiles.Select(file => file.Path).ToArray(),
                            mergedWavFile.Path
                        ),
                    cancellationToken
                );

                sourceFile = mergedWavFile;
            }

            processedWavFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                $"voice-processed-{Guid.NewGuid():N}.wav",
                CreationCollisionOption.GenerateUniqueName
            );

            try
            {
                await Task.Run(
                    () => WavVoiceLimiter.Process(sourceFile.Path, processedWavFile.Path),
                    cancellationToken
                );
                sourceFile = processedWavFile;
            }
            catch
            {
                await SafeDeleteFileAsync(processedWavFile);
                processedWavFile = null;
            }

            outputFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                $"voice-output-{Guid.NewGuid():N}.m4a",
                CreationCollisionOption.GenerateUniqueName
            );

            var transcoder = new MediaTranscoder();
            var prepareResult = await transcoder.PrepareFileTranscodeAsync(
                sourceFile,
                outputFile,
                CreateOutputEncodingProfile()
            );

            if (!prepareResult.CanTranscode)
            {
                await SafeDeleteFileAsync(outputFile);
                return (
                    false,
                    null,
                    TimeSpan.Zero,
                    0,
                    "Nie udało się przygotować finalnego pliku głosówki."
                );
            }

            await prepareResult.TranscodeAsync();

            var mergedInfo = await TryReadRecordingInfoAsync(
                outputFile,
                fallbackDuration,
                cancellationToken
            );

            return (
                mergedInfo.Success,
                mergedInfo.Success ? outputFile : null,
                mergedInfo.Duration,
                mergedInfo.DurationMs,
                mergedInfo.ErrorMessage ?? "Nie udało się przygotować połączonego nagrania."
            );
        }
        catch (OperationCanceledException)
        {
            if (outputFile is not null)
            {
                await SafeDeleteFileAsync(outputFile);
            }

            throw;
        }
        catch
        {
            if (outputFile is not null)
            {
                await SafeDeleteFileAsync(outputFile);
            }

            return (
                false,
                null,
                TimeSpan.Zero,
                0,
                "Nie udało się przygotować finalnego pliku głosówki."
            );
        }
        finally
        {
            if (processedWavFile is not null)
            {
                await SafeDeleteFileAsync(processedWavFile);
            }

            if (mergedWavFile is not null)
            {
                await SafeDeleteFileAsync(mergedWavFile);
            }
        }
    }

    private void SetState(VoiceMessageRecorderState value)
    {
        if (State == value)
        {
            return;
        }

        State = value;
        RaisePropertyChanged();
        RaisePropertyChanged(nameof(HasRecording));
    }

    private void SetElapsed(TimeSpan value)
    {
        if (Elapsed == value)
        {
            return;
        }

        Elapsed = value;
        RaisePropertyChanged();
    }

    private void SetSegmentFiles(IReadOnlyList<StorageFile> files)
    {
        var changed =
            _segmentFiles.Count != files.Count
            || _segmentFiles.Select(file => file.Path).SequenceEqual(files.Select(file => file.Path))
                is false;

        _segmentFiles.Clear();
        _segmentFiles.AddRange(files);

        if (changed)
        {
            RaisePropertyChanged(nameof(SegmentCount));
            RaisePropertyChanged(nameof(HasRecording));
        }
    }

    private void SetRecordedMetadata(string? filePath, int durationMs)
    {
        var fileChanged = RecordedFilePath != filePath;
        var durationChanged = RecordedDurationMs != durationMs;

        RecordedFilePath = filePath;
        RecordedDurationMs = durationMs;

        if (fileChanged)
        {
            RaisePropertyChanged(nameof(RecordedFilePath));
            RaisePropertyChanged(nameof(HasRecording));
        }

        if (durationChanged)
        {
            RaisePropertyChanged(nameof(RecordedDurationMs));
        }
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RaiseNotification(string message, bool isError)
    {
        NotificationRaised?.Invoke(
            this,
            new VoiceMessageRecorderNotificationEventArgs(message, isError)
        );
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }

    private Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        if (
            !_dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult(null);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            })
        )
        {
            completion.TrySetException(
                new InvalidOperationException("Nie udało się przełączyć na wątek interfejsu użytkownika.")
            );
        }

        return completion.Task;
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        if (
            !_dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var result = await action();
                    completion.TrySetResult(result);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            })
        )
        {
            completion.TrySetException(
                new InvalidOperationException("Nie udało się przełączyć na wątek interfejsu użytkownika.")
            );
        }

        return completion.Task;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static string BuildCaptureFailureMessage(MediaCaptureFailedEventArgs errorEventArgs)
    {
        return string.IsNullOrWhiteSpace(errorEventArgs.Message)
            ? "Nagrywanie zostało przerwane przez system albo mikrofon przestał być dostępny."
            : "Nagrywanie zostało przerwane. Sprawdź, czy mikrofon nadal jest dostępny i spróbuj ponownie.";
    }
}
