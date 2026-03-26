using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class RadioViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_loads_availability_and_schedule()
    {
        var service = new FakeRadioService
        {
            Availability = new RadioAvailability(true, "Audycja testowa"),
            Schedule = new RadioScheduleInfo(true, "12:00 Test", null),
        };
        var viewModel = new RadioViewModel(service, new FakeAudioPlaybackRequestFactory());

        await viewModel.LoadIfNeededAsync();

        Assert.Equal("Na antenie trwa audycja interaktywna: Audycja testowa.", viewModel.LiveStatusMessage);
        Assert.Equal("12:00 Test", viewModel.ScheduleText);
        Assert.Equal("Dane Tyfloradia zostały odświeżone.", viewModel.StatusAnnouncement);
        Assert.True(viewModel.IsInteractiveBroadcastAvailable);
    }

    [Fact]
    public async Task RefreshAsync_sets_error_from_schedule_response()
    {
        var service = new FakeRadioService
        {
            Availability = new RadioAvailability(false, null),
            Schedule = new RadioScheduleInfo(false, null, "Brak ramówki"),
        };
        var viewModel = new RadioViewModel(service, new FakeAudioPlaybackRequestFactory());

        await viewModel.RefreshAsync();

        Assert.Equal("Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna.", viewModel.LiveStatusMessage);
        Assert.Equal("Brak ramówki", viewModel.ErrorMessage);
        Assert.Equal("Brak ramówki", viewModel.StatusAnnouncement);
        Assert.False(viewModel.IsInteractiveBroadcastAvailable);
    }

    [Fact]
    public void CreatePlaybackRequest_uses_live_status_as_subtitle()
    {
        var factory = new FakeAudioPlaybackRequestFactory();
        var service = new FakeRadioService();
        var viewModel = new RadioViewModel(service, factory)
        {
            LiveStatusMessage = "Na antenie trwa audycja interaktywna: Test.",
        };

        var request = viewModel.CreatePlaybackRequest();

        Assert.Equal("Na antenie trwa audycja interaktywna: Test.", factory.LastRadioSubtitle);
        Assert.Equal("Tyfloradio", request.Title);
    }

    [Fact]
    public void TryStartContact_sets_error_when_audition_is_not_available()
    {
        var viewModel = new RadioViewModel(
            new FakeRadioService(),
            new FakeAudioPlaybackRequestFactory()
        );

        var result = viewModel.TryStartContact();

        Assert.False(result);
        Assert.Equal(
            "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna.",
            viewModel.ErrorMessage
        );
    }

    private sealed class FakeRadioService : IRadioService
    {
        public Uri LiveStreamUrl { get; init; } = new("https://radio.example/live.m3u8");

        public RadioAvailability Availability { get; init; } = new(false, null);

        public RadioScheduleInfo Schedule { get; init; } = new(false, null, null);

        public Task<RadioAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Availability);
        }

        public Task<RadioScheduleInfo> GetScheduleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Schedule);
        }
    }

    private sealed class FakeAudioPlaybackRequestFactory : IAudioPlaybackRequestFactory
    {
        public string? LastRadioSubtitle { get; private set; }

        public Uri CreatePodcastDownloadUri(int postId)
        {
            return new Uri($"https://audio.example/podcast/{postId}.mp3");
        }

        public AudioPlaybackRequest CreatePodcast(
            int postId,
            string title,
            string? subtitle = null,
            double? initialSeekSeconds = null
        )
        {
            return new AudioPlaybackRequest(
                "Podcast",
                title,
                subtitle,
                CreatePodcastDownloadUri(postId),
                false,
                true,
                true,
                InitialSeekSeconds: initialSeekSeconds
            );
        }

        public AudioPlaybackRequest CreateRadio(string? subtitle = null)
        {
            LastRadioSubtitle = subtitle;
            return new AudioPlaybackRequest(
                "Tyfloradio",
                "Tyfloradio",
                subtitle,
                new Uri("https://audio.example/live.m3u8"),
                true,
                false,
                false
            );
        }
    }
}
