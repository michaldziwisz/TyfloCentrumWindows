using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_populates_device_lists_and_selected_values()
    {
        var settingsService = new FakeAppSettingsService
        {
            Snapshot = new AppSettingsSnapshot(
                "mic-2",
                "speaker-2",
                @"D:\Tyflo\Pobrane",
                1.25,
                true,
                1.5,
                false,
                true,
                true,
                65d
            ),
        };
        var deviceCatalogService = new FakeAudioDeviceCatalogService
        {
            InputDevices =
            [
                new AudioDeviceInfo("mic-1", "Mikrofon 1"),
                new AudioDeviceInfo("mic-2", "Mikrofon 2"),
            ],
            OutputDevices =
            [
                new AudioDeviceInfo("speaker-1", "Głośnik 1"),
                new AudioDeviceInfo("speaker-2", "Głośnik 2"),
            ],
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            deviceCatalogService,
            new FakeDownloadDirectoryService()
        );

        await viewModel.LoadIfNeededAsync();

        Assert.Equal("mic-2", viewModel.SelectedInputDevice?.DeviceId);
        Assert.Equal("speaker-2", viewModel.SelectedOutputDevice?.DeviceId);
        Assert.Equal(@"D:\Tyflo\Pobrane", viewModel.DownloadDirectoryPath);
        Assert.Equal(1.25, viewModel.SelectedDefaultPlaybackRate?.Value);
        Assert.True(viewModel.RememberLastPlaybackRate);
        Assert.True(viewModel.RememberLastPlaybackVolume);
        Assert.False(viewModel.NotifyAboutNewPodcasts);
        Assert.True(viewModel.NotifyAboutNewArticles);
        Assert.Equal("Ostatnio zapamiętana prędkość: 1,5x.", viewModel.RememberedPlaybackRateDescription);
        Assert.Equal("Ostatnio zapamiętana głośność: 65%.", viewModel.RememberedPlaybackVolumeDescription);
        Assert.Equal(3, viewModel.InputDevices.Count);
        Assert.Equal(3, viewModel.OutputDevices.Count);
        Assert.Equal("Ustawienia zostały wczytane.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ResetAudioSettingsAsync_restores_defaults_and_clears_remembered_rate()
    {
        var settingsService = new FakeAppSettingsService
        {
            Snapshot = new AppSettingsSnapshot(
                "mic-1",
                "speaker-1",
                @"D:\Tyflo\Pobrane",
                1.25,
                true,
                1.5,
                false,
                true,
                true,
                65d
            ),
        };
        var deviceCatalogService = new FakeAudioDeviceCatalogService
        {
            InputDevices = [new AudioDeviceInfo("mic-1", "Mikrofon 1")],
            OutputDevices = [new AudioDeviceInfo("speaker-1", "Głośnik 1")],
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            deviceCatalogService,
            new FakeDownloadDirectoryService()
        );
        await viewModel.LoadIfNeededAsync();

        await viewModel.ResetAudioSettingsAsync();

        Assert.Null(settingsService.LastSavedSnapshot?.PreferredInputDeviceId);
        Assert.Null(settingsService.LastSavedSnapshot?.PreferredOutputDeviceId);
        Assert.Equal(@"D:\Tyflo\Pobrane", settingsService.LastSavedSnapshot?.DownloadDirectoryPath);
        Assert.Equal(
            PlaybackRateCatalog.DefaultValue,
            settingsService.LastSavedSnapshot?.DefaultPlaybackRate
        );
        Assert.False(settingsService.LastSavedSnapshot?.RememberLastPlaybackRate);
        Assert.Null(settingsService.LastSavedSnapshot?.LastPlaybackRate);
        Assert.False(settingsService.LastSavedSnapshot?.RememberLastPlaybackVolume);
        Assert.Null(settingsService.LastSavedSnapshot?.LastPlaybackVolumePercent);
        Assert.False(settingsService.LastSavedSnapshot?.NotifyAboutNewPodcasts);
        Assert.True(settingsService.LastSavedSnapshot?.NotifyAboutNewArticles);
        Assert.Equal("Przywrócono domyślne ustawienia audio.", viewModel.StatusMessage);
        Assert.False(viewModel.HasRememberedPlaybackRate);
        Assert.False(viewModel.HasRememberedPlaybackVolume);
    }

    [Fact]
    public async Task ClearRememberedPlaybackVolumeAsync_clears_volume_and_updates_status()
    {
        var settingsService = new FakeAppSettingsService
        {
            Snapshot = new AppSettingsSnapshot(
                "mic-1",
                "speaker-1",
                null,
                PlaybackRateCatalog.DefaultValue,
                false,
                null,
                true,
                true,
                true,
                35d
            ),
        };
        var deviceCatalogService = new FakeAudioDeviceCatalogService
        {
            InputDevices = [new AudioDeviceInfo("mic-1", "Mikrofon 1")],
            OutputDevices = [new AudioDeviceInfo("speaker-1", "Głośnik 1")],
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            deviceCatalogService,
            new FakeDownloadDirectoryService()
        );
        await viewModel.LoadIfNeededAsync();

        await viewModel.ClearRememberedPlaybackVolumeAsync();

        Assert.Null(settingsService.LastSavedSnapshot?.LastPlaybackVolumePercent);
        Assert.Equal("Wyczyszczono zapamiętaną głośność odtwarzania.", viewModel.StatusMessage);
        Assert.False(viewModel.HasRememberedPlaybackVolume);
    }

    private sealed class FakeAppSettingsService : IAppSettingsService
    {
        public AppSettingsSnapshot Snapshot { get; set; } = AppSettingsSnapshot.Defaults;

        public AppSettingsSnapshot? LastSavedSnapshot { get; private set; }

        public Task<AppSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshot);
        }

        public Task SaveAsync(
            AppSettingsSnapshot settings,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSavedSnapshot = settings.Normalize();
            Snapshot = LastSavedSnapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudioDeviceCatalogService : IAudioDeviceCatalogService
    {
        public IReadOnlyList<AudioDeviceInfo> InputDevices { get; init; } = [];

        public IReadOnlyList<AudioDeviceInfo> OutputDevices { get; init; } = [];

        public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(InputDevices);
        }

        public Task<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OutputDevices);
        }
    }

    private sealed class FakeDownloadDirectoryService : IDownloadDirectoryService
    {
        public string GetDefaultDownloadDirectoryPath()
        {
            return @"C:\Users\Test\Downloads";
        }

        public string GetEffectiveDownloadDirectoryPath(string? configuredPath)
        {
            return string.IsNullOrWhiteSpace(configuredPath)
                ? GetDefaultDownloadDirectoryPath()
                : configuredPath.Trim();
        }
    }
}
