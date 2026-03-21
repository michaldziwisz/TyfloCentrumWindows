using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContactTextMessageViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_restores_name_and_uses_default_message_when_draft_is_empty()
    {
        var store = new FakeLocalSettingsStore
        {
            ["contact.radio.name"] = "Alicja",
            ["contact.radio.message"] = string.Empty,
        };
        var viewModel = new ContactTextMessageViewModel(new FakeRadioContactService(), store);

        await viewModel.LoadIfNeededAsync();

        Assert.Equal("Alicja", viewModel.Name);
        Assert.Contains("TyfloCentrum", viewModel.Message, StringComparison.Ordinal);
        Assert.True(viewModel.CanSend);
    }

    [Fact]
    public async Task SendAsync_resets_message_and_raises_sent_event_on_success()
    {
        var service = new FakeRadioContactService
        {
            Result = new ContactSubmissionResult(true, null),
        };
        var store = new FakeLocalSettingsStore();
        var viewModel = new ContactTextMessageViewModel(service, store)
        {
            Name = "UI",
            Message = "Wiadomość testowa",
        };
        var wasSent = false;
        viewModel.MessageSent += (_, _) => wasSent = true;

        var result = await viewModel.SendAsync();

        Assert.True(result);
        Assert.True(wasSent);
        Assert.Equal("UI", service.LastAuthor);
        Assert.Equal("Wiadomość testowa", service.LastComment);
        Assert.Contains("TyfloCentrum", viewModel.Message, StringComparison.Ordinal);
        Assert.Equal("Wiadomość wysłana pomyślnie.", viewModel.StatusMessage);
        Assert.Equal(viewModel.Message, store["contact.radio.message"]);
    }

    [Fact]
    public async Task SendAsync_shows_api_error_message_when_service_reports_failure()
    {
        var viewModel = new ContactTextMessageViewModel(
            new FakeRadioContactService
            {
                Result = new ContactSubmissionResult(false, "Błąd wysyłki"),
            },
            new FakeLocalSettingsStore()
        )
        {
            Name = "UI",
            Message = "Wiadomość testowa",
        };

        var result = await viewModel.SendAsync();

        Assert.False(result);
        Assert.Equal("Błąd wysyłki", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
    }

    private sealed class FakeRadioContactService : IRadioContactService
    {
        public ContactSubmissionResult Result { get; init; } = new(true, null);

        public string? LastAuthor { get; private set; }

        public string? LastComment { get; private set; }

        public Task<ContactSubmissionResult> SendMessageAsync(
            string author,
            string comment,
            CancellationToken cancellationToken = default
        )
        {
            LastAuthor = author;
            LastComment = comment;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeLocalSettingsStore : ILocalSettingsStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set
            {
                if (value is null)
                {
                    _values.Remove(key);
                    return;
                }

                _values[key] = value;
            }
        }

        public ValueTask<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(this[key]);
        }

        public ValueTask SetStringAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this[key] = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteStringAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.Remove(key);
            return ValueTask.CompletedTask;
        }
    }
}
