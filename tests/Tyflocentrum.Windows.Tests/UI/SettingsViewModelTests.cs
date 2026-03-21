using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.UI.ViewModels;
using Xunit;

namespace Tyflocentrum.Windows.Tests.UI;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_populates_device_lists_and_selected_values()
    {
        var settingsService = new FakeAppSettingsService
        {
            Snapshot = new AppSettingsSnapshot("mic-2", "speaker-2", 1.25, true, 1.5),
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
        var viewModel = new SettingsViewModel(settingsService, deviceCatalogService);

        await viewModel.LoadIfNeededAsync();

        Assert.Equal("mic-2", viewModel.SelectedInputDevice?.DeviceId);
        Assert.Equal("speaker-2", viewModel.SelectedOutputDevice?.DeviceId);
        Assert.Equal(1.25, viewModel.SelectedDefaultPlaybackRate?.Value);
        Assert.True(viewModel.RememberLastPlaybackRate);
        Assert.Equal("Ostatnio zapamiętana prędkość: 1,5x.", viewModel.RememberedPlaybackRateDescription);
        Assert.Equal(3, viewModel.InputDevices.Count);
        Assert.Equal(3, viewModel.OutputDevices.Count);
        Assert.Equal("Ustawienia zostały wczytane.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ResetAudioSettingsAsync_restores_defaults_and_clears_remembered_rate()
    {
        var settingsService = new FakeAppSettingsService
        {
            Snapshot = new AppSettingsSnapshot("mic-1", "speaker-1", 1.25, true, 1.5),
        };
        var deviceCatalogService = new FakeAudioDeviceCatalogService
        {
            InputDevices = [new AudioDeviceInfo("mic-1", "Mikrofon 1")],
            OutputDevices = [new AudioDeviceInfo("speaker-1", "Głośnik 1")],
        };
        var viewModel = new SettingsViewModel(settingsService, deviceCatalogService);
        await viewModel.LoadIfNeededAsync();

        await viewModel.ResetAudioSettingsAsync();

        Assert.Null(settingsService.LastSavedSnapshot?.PreferredInputDeviceId);
        Assert.Null(settingsService.LastSavedSnapshot?.PreferredOutputDeviceId);
        Assert.Equal(
            PlaybackRateCatalog.DefaultValue,
            settingsService.LastSavedSnapshot?.DefaultPlaybackRate
        );
        Assert.False(settingsService.LastSavedSnapshot?.RememberLastPlaybackRate);
        Assert.Null(settingsService.LastSavedSnapshot?.LastPlaybackRate);
        Assert.Equal("Przywrócono domyślne ustawienia audio.", viewModel.StatusMessage);
        Assert.False(viewModel.HasRememberedPlaybackRate);
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
}
