using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;
    private bool _hasLoaded;
    private double? _lastPlaybackRate;

    public SettingsViewModel(
        IAppSettingsService appSettingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService
    )
    {
        _appSettingsService = appSettingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;

        foreach (var value in PlaybackRateCatalog.SupportedValues)
        {
            PlaybackRates.Add(new PlaybackRateOptionViewModel(value, PlaybackRateCatalog.FormatLabel(value)));
        }
    }

    public ObservableCollection<AudioDeviceOptionViewModel> InputDevices { get; } = [];

    public ObservableCollection<AudioDeviceOptionViewModel> OutputDevices { get; } = [];

    public ObservableCollection<PlaybackRateOptionViewModel> PlaybackRates { get; } = [];

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
    private bool rememberLastPlaybackRate;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanSave =>
        !IsLoading
        && !IsSaving
        && SelectedInputDevice is not null
        && SelectedOutputDevice is not null
        && SelectedDefaultPlaybackRate is not null;

    public bool CanRefreshDevices => !IsLoading && !IsSaving;

    public bool CanResetAudioSettings => !IsLoading && !IsSaving;

    public bool HasRememberedPlaybackRate => _lastPlaybackRate.HasValue;

    public bool CanClearRememberedPlaybackRate => !IsLoading && !IsSaving && HasRememberedPlaybackRate;

    public string RememberedPlaybackRateDescription => _lastPlaybackRate is double value
        ? $"Ostatnio zapamiętana prędkość: {PlaybackRateCatalog.FormatLabel(value)}."
        : "Brak zapamiętanej prędkości odtwarzania.";

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
            RememberLastPlaybackRate = settings.RememberLastPlaybackRate;
            _lastPlaybackRate = settings.LastPlaybackRate is null
                ? null
                : PlaybackRateCatalog.Coerce(settings.LastPlaybackRate.Value);

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
            await _appSettingsService.SaveAsync(CreateSnapshot(), cancellationToken);
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
        _lastPlaybackRate = null;
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

    partial void OnRememberLastPlaybackRateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    private AppSettingsSnapshot CreateSnapshot()
    {
        return new AppSettingsSnapshot(
            SelectedInputDevice?.DeviceId,
            SelectedOutputDevice?.DeviceId,
            SelectedDefaultPlaybackRate?.Value ?? PlaybackRateCatalog.DefaultValue,
            RememberLastPlaybackRate,
            _lastPlaybackRate
        ).Normalize();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanRefreshDevices));
        OnPropertyChanged(nameof(CanResetAudioSettings));
        OnPropertyChanged(nameof(HasRememberedPlaybackRate));
        OnPropertyChanged(nameof(CanClearRememberedPlaybackRate));
        OnPropertyChanged(nameof(RememberedPlaybackRateDescription));
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
