using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;
    private readonly IAppRuntimeMode _appRuntimeMode;
    private readonly ContentTypeAnnouncementPreferenceService _contentTypeAnnouncementPreferenceService;
    private readonly IDownloadDirectoryService _downloadDirectoryService;
    private bool _hasLoaded;
    private double? _lastPlaybackRate;
    private double? _lastPlaybackVolumePercent;

    public SettingsViewModel(
        IAppSettingsService appSettingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService,
        IDownloadDirectoryService downloadDirectoryService,
        ContentTypeAnnouncementPreferenceService contentTypeAnnouncementPreferenceService,
        IAppRuntimeMode appRuntimeMode
    )
    {
        _appSettingsService = appSettingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
        _downloadDirectoryService = downloadDirectoryService;
        _contentTypeAnnouncementPreferenceService = contentTypeAnnouncementPreferenceService;
        _appRuntimeMode = appRuntimeMode;

        foreach (var value in PlaybackRateCatalog.SupportedValues)
        {
            PlaybackRates.Add(new PlaybackRateOptionViewModel(value, PlaybackRateCatalog.FormatLabel(value)));
        }

        ContentTypeAnnouncementPlacements.Add(
            new ContentTypeAnnouncementPlacementOptionViewModel(
                ContentTypeAnnouncementPlacement.None,
                "Nie wskazuj typu treści"
            )
        );
        ContentTypeAnnouncementPlacements.Add(
            new ContentTypeAnnouncementPlacementOptionViewModel(
                ContentTypeAnnouncementPlacement.BeforeTitle,
                "Wskazuj typ treści przed nazwą"
            )
        );
        ContentTypeAnnouncementPlacements.Add(
            new ContentTypeAnnouncementPlacementOptionViewModel(
                ContentTypeAnnouncementPlacement.AfterTitle,
                "Wskazuj typ treści po nazwie"
            )
        );
    }

    public ObservableCollection<AudioDeviceOptionViewModel> InputDevices { get; } = [];

    public ObservableCollection<AudioDeviceOptionViewModel> OutputDevices { get; } = [];

    public ObservableCollection<PlaybackRateOptionViewModel> PlaybackRates { get; } = [];

    public ObservableCollection<ContentTypeAnnouncementPlacementOptionViewModel>
        ContentTypeAnnouncementPlacements { get; } = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private AudioDeviceOptionViewModel? selectedInputDevice;

    [ObservableProperty]
    private AudioDeviceOptionViewModel? selectedOutputDevice;

    [ObservableProperty]
    private PlaybackRateOptionViewModel? selectedDefaultPlaybackRate;

    [ObservableProperty]
    private string? downloadDirectoryPath;

    [ObservableProperty]
    private bool rememberLastPlaybackRate;

    [ObservableProperty]
    private bool rememberLastPlaybackVolume;

    [ObservableProperty]
    private bool notifyAboutNewPodcasts;

    [ObservableProperty]
    private bool notifyAboutNewArticles;

    [ObservableProperty]
    private ContentTypeAnnouncementPlacementOptionViewModel? selectedContentTypeAnnouncementPlacement;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanSave =>
        !IsLoading
        && !IsSaving
        && SelectedInputDevice is not null
        && SelectedOutputDevice is not null
        && SelectedDefaultPlaybackRate is not null
        && SelectedContentTypeAnnouncementPlacement is not null;

    public bool CanRefreshDevices => !IsLoading && !IsSaving;

    public bool CanResetAudioSettings => !IsLoading && !IsSaving;

    public bool CanChooseDownloadDirectory => !IsLoading && !IsSaving;

    public bool HasRememberedPlaybackRate => _lastPlaybackRate.HasValue;

    public bool CanClearRememberedPlaybackRate => !IsLoading && !IsSaving && HasRememberedPlaybackRate;

    public bool HasRememberedPlaybackVolume => _lastPlaybackVolumePercent.HasValue;

    public bool CanClearRememberedPlaybackVolume =>
        !IsLoading && !IsSaving && HasRememberedPlaybackVolume;

    public string RememberedPlaybackRateDescription => _lastPlaybackRate is double value
        ? $"Ostatnio zapamiętana prędkość: {PlaybackRateCatalog.FormatLabel(value)}."
        : "Brak zapamiętanej prędkości odtwarzania.";

    public string RememberedPlaybackVolumeDescription => _lastPlaybackVolumePercent is double value
        ? $"Ostatnio zapamiętana głośność: {value:0}%."
        : "Brak zapamiętanej głośności odtwarzania.";

    public string EffectiveDownloadDirectoryDescription
    {
        get
        {
            var effectivePath = _downloadDirectoryService.GetEffectiveDownloadDirectoryPath(
                DownloadDirectoryPath
            );
            return string.IsNullOrWhiteSpace(DownloadDirectoryPath)
                ? $"Domyślny folder pobierania systemu Windows: {effectivePath}."
                : $"Pobrane pliki będą zapisywane w folderze: {effectivePath}.";
        }
    }

    public string NotificationsDescription =>
        _appRuntimeMode.SupportsSystemNotifications
            ? "Powiadomienia o nowych artykułach i podcastach pojawiają się, gdy aplikacja jest uruchomiona na tym komputerze."
            : "W tej wersji instalowanej bez MSIX powiadomienia systemowe i WNS są niedostępne.";

    public bool SupportsSystemNotifications => _appRuntimeMode.SupportsSystemNotifications;

    public string ContentTypeAnnouncementDescription =>
        "To ustawienie wpływa na sposób, w jaki czytnik ekranu odczytuje pozycje na listach nowości, podcastów, artykułów i wyników wyszukiwania.";

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Ładowanie ustawień…";
        NotifyStateChanged();

        try
        {
            var settingsTask = _appSettingsService.GetAsync(cancellationToken);
            var inputDevicesTask = _audioDeviceCatalogService.GetInputDevicesAsync(cancellationToken);
            var outputDevicesTask = _audioDeviceCatalogService.GetOutputDevicesAsync(cancellationToken);

            await Task.WhenAll(settingsTask, inputDevicesTask, outputDevicesTask);

            var settings = settingsTask.Result.Normalize();
            var inputDevices = BuildDeviceOptions(inputDevicesTask.Result, "Domyślne urządzenie wejściowe systemu");
            var outputDevices = BuildDeviceOptions(outputDevicesTask.Result, "Domyślne urządzenie wyjściowe systemu");

            ReplaceItems(InputDevices, inputDevices);
            ReplaceItems(OutputDevices, outputDevices);

            SelectedInputDevice = SelectDeviceOption(InputDevices, settings.PreferredInputDeviceId);
            SelectedOutputDevice = SelectDeviceOption(OutputDevices, settings.PreferredOutputDeviceId);
            SelectedDefaultPlaybackRate = SelectPlaybackRate(settings.DefaultPlaybackRate);
            DownloadDirectoryPath = settings.DownloadDirectoryPath;
            RememberLastPlaybackRate = settings.RememberLastPlaybackRate;
            RememberLastPlaybackVolume = settings.RememberLastPlaybackVolume;
            NotifyAboutNewPodcasts = settings.NotifyAboutNewPodcasts;
            NotifyAboutNewArticles = settings.NotifyAboutNewArticles;
            SelectedContentTypeAnnouncementPlacement =
                SelectContentTypeAnnouncementPlacement(settings.ContentTypeAnnouncementPlacement);
            _lastPlaybackRate = settings.LastPlaybackRate is null
                ? null
                : PlaybackRateCatalog.Coerce(settings.LastPlaybackRate.Value);
            _lastPlaybackVolumePercent = settings.LastPlaybackVolumePercent is null
                ? null
                : Math.Clamp(settings.LastPlaybackVolumePercent.Value, 0d, 100d);
            _contentTypeAnnouncementPreferenceService.SetPlacement(
                settings.ContentTypeAnnouncementPlacement
            );

            HasLoadedOnce = true;
            StatusMessage = BuildLoadStatusMessage(settings);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ErrorMessage = "Nie udało się wczytać ustawień. Spróbuj ponownie.";
            StatusMessage = ErrorMessage;
            HasLoadedOnce = true;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave)
        {
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = "Zapisywanie ustawień…";
        NotifyStateChanged();

        try
        {
            var snapshot = CreateSnapshot();
            await _appSettingsService.SaveAsync(snapshot, cancellationToken);
            _contentTypeAnnouncementPreferenceService.SetPlacement(
                snapshot.ContentTypeAnnouncementPlacement
            );
            StatusMessage =
                "Ustawienia zapisane. Nowe urządzenia będą użyte przy kolejnym nagraniu lub otwarciu odtwarzacza.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ErrorMessage = "Nie udało się zapisać ustawień. Spróbuj ponownie.";
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public async Task ResetAudioSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanResetAudioSettings)
        {
            return;
        }

        SelectedInputDevice = InputDevices.FirstOrDefault();
        SelectedOutputDevice = OutputDevices.FirstOrDefault();
        SelectedDefaultPlaybackRate = SelectPlaybackRate(PlaybackRateCatalog.DefaultValue);
        RememberLastPlaybackRate = false;
        RememberLastPlaybackVolume = false;
        _lastPlaybackRate = null;
        _lastPlaybackVolumePercent = null;
        NotifyStateChanged();

        await SaveAsync(cancellationToken);
        if (!HasError)
        {
            StatusMessage = "Przywrócono domyślne ustawienia audio.";
            NotifyStateChanged();
        }
    }

    public async Task ClearRememberedPlaybackRateAsync(CancellationToken cancellationToken = default)
    {
        if (!CanClearRememberedPlaybackRate)
        {
            return;
        }

        _lastPlaybackRate = null;
        NotifyStateChanged();
        await SaveAsync(cancellationToken);

        if (!HasError)
        {
            StatusMessage = "Wyczyszczono zapamiętaną prędkość odtwarzania.";
            NotifyStateChanged();
        }
    }

    public async Task ClearRememberedPlaybackVolumeAsync(CancellationToken cancellationToken = default)
    {
        if (!CanClearRememberedPlaybackVolume)
        {
            return;
        }

        _lastPlaybackVolumePercent = null;
        NotifyStateChanged();
        await SaveAsync(cancellationToken);

        if (!HasError)
        {
            StatusMessage = "Wyczyszczono zapamiętaną głośność odtwarzania.";
            NotifyStateChanged();
        }
    }

    public void UpdateRememberedPlaybackRate(double? value)
    {
        _lastPlaybackRate = value is null ? null : PlaybackRateCatalog.Coerce(value.Value);
        NotifyStateChanged();
    }

    partial void OnSelectedInputDeviceChanged(AudioDeviceOptionViewModel? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnSelectedOutputDeviceChanged(AudioDeviceOptionViewModel? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnSelectedDefaultPlaybackRateChanged(PlaybackRateOptionViewModel? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnDownloadDirectoryPathChanged(string? value)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(EffectiveDownloadDirectoryDescription));
    }

    partial void OnRememberLastPlaybackRateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnRememberLastPlaybackVolumeChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnNotifyAboutNewPodcastsChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnNotifyAboutNewArticlesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnSelectedContentTypeAnnouncementPlacementChanged(
        ContentTypeAnnouncementPlacementOptionViewModel? value
    )
    {
        OnPropertyChanged(nameof(CanSave));
    }

    private AppSettingsSnapshot CreateSnapshot()
    {
        return new AppSettingsSnapshot(
            SelectedInputDevice?.DeviceId,
            SelectedOutputDevice?.DeviceId,
            NormalizeDownloadDirectoryPath(DownloadDirectoryPath),
            SelectedDefaultPlaybackRate?.Value ?? PlaybackRateCatalog.DefaultValue,
            RememberLastPlaybackRate,
            _lastPlaybackRate,
            NotifyAboutNewPodcasts,
            NotifyAboutNewArticles,
            RememberLastPlaybackVolume,
            _lastPlaybackVolumePercent,
            SelectedContentTypeAnnouncementPlacement?.Value ?? ContentTypeAnnouncementPlacement.None
        ).Normalize();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanRefreshDevices));
        OnPropertyChanged(nameof(CanResetAudioSettings));
        OnPropertyChanged(nameof(CanChooseDownloadDirectory));
        OnPropertyChanged(nameof(HasRememberedPlaybackRate));
        OnPropertyChanged(nameof(CanClearRememberedPlaybackRate));
        OnPropertyChanged(nameof(RememberedPlaybackRateDescription));
        OnPropertyChanged(nameof(HasRememberedPlaybackVolume));
        OnPropertyChanged(nameof(CanClearRememberedPlaybackVolume));
        OnPropertyChanged(nameof(RememberedPlaybackVolumeDescription));
        OnPropertyChanged(nameof(EffectiveDownloadDirectoryDescription));
    }

    private static string? NormalizeDownloadDirectoryPath(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private static IReadOnlyList<AudioDeviceOptionViewModel> BuildDeviceOptions(
        IReadOnlyList<AudioDeviceInfo> devices,
        string defaultLabel
    )
    {
        var options = new List<AudioDeviceOptionViewModel>
        {
            new(null, defaultLabel),
        };

        options.AddRange(
            devices
                .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(device => new AudioDeviceOptionViewModel(device.Id, device.Name))
        );

        return options;
    }

    private AudioDeviceOptionViewModel SelectDeviceOption(
        IEnumerable<AudioDeviceOptionViewModel> options,
        string? deviceId
    )
    {
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var matchingOption = options.FirstOrDefault(option =>
                string.Equals(option.DeviceId, deviceId, StringComparison.Ordinal)
            );

            if (matchingOption is not null)
            {
                return matchingOption;
            }
        }

        return options.First();
    }

    private ContentTypeAnnouncementPlacementOptionViewModel SelectContentTypeAnnouncementPlacement(
        ContentTypeAnnouncementPlacement value
    )
    {
        return ContentTypeAnnouncementPlacements.FirstOrDefault(option => option.Value == value)
            ?? ContentTypeAnnouncementPlacements[0];
    }

    private PlaybackRateOptionViewModel SelectPlaybackRate(double value)
    {
        var normalizedValue = PlaybackRateCatalog.Coerce(value);
        return PlaybackRates.First(option => option.Value == normalizedValue);
    }

    private string BuildLoadStatusMessage(AppSettingsSnapshot settings)
    {
        var messages = new List<string>();

        if (
            !string.IsNullOrWhiteSpace(settings.PreferredInputDeviceId)
            && InputDevices.All(option =>
                !string.Equals(option.DeviceId, settings.PreferredInputDeviceId, StringComparison.Ordinal)
            )
        )
        {
            messages.Add(
                "Zapisane urządzenie wejściowe nie jest teraz dostępne. Aplikacja użyje domyślnego urządzenia systemowego."
            );
        }

        if (
            !string.IsNullOrWhiteSpace(settings.PreferredOutputDeviceId)
            && OutputDevices.All(option =>
                !string.Equals(option.DeviceId, settings.PreferredOutputDeviceId, StringComparison.Ordinal)
            )
        )
        {
            messages.Add(
                "Zapisane urządzenie wyjściowe nie jest teraz dostępne. Odtwarzacz użyje domyślnego urządzenia systemowego dopiero po zapisaniu zmian."
            );
        }

        return messages.Count == 0
            ? "Ustawienia zostały wczytane."
            : string.Join(" ", messages);
    }
}
