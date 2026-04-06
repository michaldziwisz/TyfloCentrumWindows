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
        Assert.Equal(
            "Status audycji interaktywnej. Na antenie trwa audycja interaktywna: Audycja testowa.",
            viewModel.LiveStatusAccessibleText
        );
        Assert.Equal("Ramówka. 12:00 Test", viewModel.ScheduleAccessibleText);
        Assert.True(viewModel.IsInteractiveBroadcastAvailable);
    }

    [Fact]
    public async Task LoadIfNeededAsync_normalizes_schedule_line_breaks_and_html_breaks()
    {
        var service = new FakeRadioService
        {
            Availability = new RadioAvailability(true, "Audycja testowa"),
            Schedule = new RadioScheduleInfo(
                true,
                "12:00 Start<br>13:00 Dalej\\n14:00 Koniec",
                null
            ),
        };
        var viewModel = new RadioViewModel(service, new FakeAudioPlaybackRequestFactory());

        await viewModel.LoadIfNeededAsync();

        Assert.Equal("12:00 Start\n13:00 Dalej\n14:00 Koniec", viewModel.ScheduleText);
        Assert.Equal(
            "Ramówka. 12:00 Start\n13:00 Dalej\n14:00 Koniec",
            viewModel.ScheduleAccessibleText
        );
    }

    [Fact]
    public async Task LoadIfNeededAsync_preserves_plain_multiline_schedule_text()
    {
        var service = new FakeRadioService
        {
            Availability = new RadioAvailability(false, null),
            Schedule = new RadioScheduleInfo(
                true,
                "Witajcie,\nw tym tygodniu jedyną audycją na antenie TyfloRadia będzie\nTyfloPrzegląd.\nDo usłyszenia!\n",
                null
            ),
        };
        var viewModel = new RadioViewModel(service, new FakeAudioPlaybackRequestFactory());

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(
            "Witajcie,\nw tym tygodniu jedyną audycją na antenie TyfloRadia będzie\nTyfloPrzegląd.\nDo usłyszenia!",
            viewModel.ScheduleText
        );
        Assert.Equal(viewModel.ScheduleText, viewModel.ScheduleDisplayText);
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
        Assert.Equal("Brak ramówki", viewModel.ScheduleDisplayText);
        Assert.True(viewModel.CanOpenSchedule);
        Assert.False(viewModel.IsInteractiveBroadcastAvailable);
    }

    [Fact]
    public async Task RefreshAsync_exposes_schedule_fallback_in_readonly_field_text()
    {
        var service = new FakeRadioService
        {
            Availability = new RadioAvailability(false, null),
            Schedule = new RadioScheduleInfo(true, null, null),
        };
        var viewModel = new RadioViewModel(service, new FakeAudioPlaybackRequestFactory());

        await viewModel.RefreshAsync();

        Assert.Equal("Brak dostępnej ramówki.", viewModel.ScheduleDisplayText);
        Assert.True(viewModel.CanOpenSchedule);
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
        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(
            "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna, więc nie można wysłać wiadomości tekstowej.",
            viewModel.StatusAnnouncement
        );
    }

    [Fact]
    public void TryStartVoiceContact_sets_voice_specific_error_when_audition_is_not_available()
    {
        var viewModel = new RadioViewModel(
            new FakeRadioService(),
            new FakeAudioPlaybackRequestFactory()
        );

        var result = viewModel.TryStartVoiceContact();

        Assert.False(result);
        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(
            "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna, więc nie można nagrać głosówki.",
            viewModel.StatusAnnouncement
        );
    }

    [Fact]
    public void TryStartTextContact_does_not_clear_existing_feedback_when_audition_is_available()
    {
        var viewModel = new RadioViewModel(
            new FakeRadioService
            {
                Availability = new RadioAvailability(true, "Audycja testowa"),
            },
            new FakeAudioPlaybackRequestFactory()
        )
        {
            ErrorMessage = "Poprzedni komunikat.",
            StatusAnnouncement = "Poprzedni komunikat.",
            IsInteractiveBroadcastAvailable = true,
        };

        var result = viewModel.TryStartTextContact();

        Assert.True(result);
        Assert.Equal("Poprzedni komunikat.", viewModel.ErrorMessage);
        Assert.Equal("Poprzedni komunikat.", viewModel.StatusAnnouncement);
    }

    [Fact]
    public void ReportContactFormOpenError_announces_without_setting_error_bar_state()
    {
        var viewModel = new RadioViewModel(
            new FakeRadioService(),
            new FakeAudioPlaybackRequestFactory()
        );

        viewModel.ReportContactFormOpenError();

        Assert.False(viewModel.HasError);
        Assert.Equal(
            "Nie udało się otworzyć formularza wiadomości do Tyfloradia.",
            viewModel.StatusAnnouncement
        );
    }

    [Fact]
    public void TryStartTextContact_reannounces_same_error_on_second_attempt()
    {
        var viewModel = new RadioViewModel(
            new FakeRadioService(),
            new FakeAudioPlaybackRequestFactory()
        );
        var statusAnnouncementChanges = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RadioViewModel.StatusAnnouncement))
            {
                statusAnnouncementChanges++;
            }
        };

        _ = viewModel.TryStartTextContact();
        _ = viewModel.TryStartTextContact();

        Assert.True(statusAnnouncementChanges >= 3);
        Assert.Equal(
            "Na antenie Tyfloradia nie trwa teraz żadna audycja interaktywna, więc nie można wysłać wiadomości tekstowej.",
            viewModel.StatusAnnouncement
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
