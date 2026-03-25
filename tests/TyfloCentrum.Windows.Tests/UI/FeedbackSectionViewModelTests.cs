using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class FeedbackSectionViewModelTests
{
    [Fact]
    public async Task SubmitAsync_returns_false_when_required_fields_are_missing()
    {
        var viewModel = new FeedbackSectionViewModel(
            new FakeFeedbackSubmissionService(),
            new FakeExternalLinkLauncher()
        );

        var result = await viewModel.SubmitAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task SubmitAsync_sends_selected_kind_and_flags()
    {
        var service = new FakeFeedbackSubmissionService
        {
            Result = new FeedbackSubmissionResult(
                true,
                null,
                "https://github.com/example/repo/issues/5",
                "report-5"
            ),
        };
        var viewModel = new FeedbackSectionViewModel(service, new FakeExternalLinkLauncher())
        {
            SelectedKindOption = new FeedbackKindOptionViewModel(
                FeedbackSubmissionKind.Suggestion,
                "Sugestia"
            ),
            Title = "Nowa funkcja",
            Description = "Przydałby się krótszy skrót.",
            IncludeDiagnostics = true,
            IncludeLogFile = true,
        };

        var result = await viewModel.SubmitAsync();

        Assert.True(result);
        Assert.NotNull(service.LastRequest);
        Assert.Equal(FeedbackSubmissionKind.Suggestion, service.LastRequest!.Kind);
        Assert.Null(service.LastRequest.ContactEmail);
        Assert.True(service.LastRequest.IncludeDiagnostics);
        Assert.True(service.LastRequest.IncludeLogFile);
        Assert.Equal(string.Empty, viewModel.Title);
        Assert.Equal(string.Empty, viewModel.Description);
        Assert.Equal(string.Empty, viewModel.ContactEmail);
        Assert.False(viewModel.AllowPrivateContactByEmail);
        Assert.Equal(
            "https://github.com/example/repo/issues/5",
            viewModel.PublicIssueUrl
        );
        Assert.True(viewModel.CanOpenPublicIssue);
    }

    [Fact]
    public async Task SubmitAsync_shows_api_error_message_on_failure()
    {
        var viewModel = new FeedbackSectionViewModel(
            new FakeFeedbackSubmissionService
            {
                Result = new FeedbackSubmissionResult(false, "Błąd serwera", null, null),
            },
            new FakeExternalLinkLauncher()
        )
        {
            Title = "Brak dźwięku",
            Description = "Po wejściu do playera nic nie słychać.",
        };

        var result = await viewModel.SubmitAsync();

        Assert.False(result);
        Assert.Equal("Błąd serwera", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task SubmitAsync_blocks_optional_email_without_explicit_consent()
    {
        var viewModel = new FeedbackSectionViewModel(
            new FakeFeedbackSubmissionService(),
            new FakeExternalLinkLauncher()
        )
        {
            Title = "Brak dźwięku",
            Description = "Po wejściu do playera nic nie słychać.",
            ContactEmail = "michal@example.com",
        };

        var result = await viewModel.SubmitAsync();

        Assert.False(result);
        Assert.Equal(
            "Aby wysłać adres e-mail do prywatnego repo diagnostycznego, zaznacz zgodę poniżej.",
            viewModel.ErrorMessage
        );
    }

    [Fact]
    public async Task SubmitAsync_sends_optional_email_only_after_opt_in()
    {
        var service = new FakeFeedbackSubmissionService();
        var viewModel = new FeedbackSectionViewModel(service, new FakeExternalLinkLauncher())
        {
            Title = "Kontakt",
            Description = "Można się ze mną skontaktować w sprawie logów.",
            ContactEmail = "michal@example.com",
            AllowPrivateContactByEmail = true,
        };

        var result = await viewModel.SubmitAsync();

        Assert.True(result);
        Assert.Equal("michal@example.com", service.LastRequest?.ContactEmail);
    }

    [Fact]
    public async Task OpenPublicIssueAsync_uses_external_launcher()
    {
        var launcher = new FakeExternalLinkLauncher();
        var viewModel = new FeedbackSectionViewModel(
            new FakeFeedbackSubmissionService(),
            launcher
        )
        {
            PublicIssueUrl = "https://github.com/example/repo/issues/11",
        };

        var result = await viewModel.OpenPublicIssueAsync();

        Assert.True(result);
        Assert.Equal("https://github.com/example/repo/issues/11", launcher.LastTarget);
    }

    private sealed class FakeFeedbackSubmissionService : IFeedbackSubmissionService
    {
        public FeedbackSubmissionResult Result { get; init; } = new(true, null, null, "report-1");

        public FeedbackSubmissionRequest? LastRequest { get; private set; }

        public Task<FeedbackSubmissionResult> SubmitAsync(
            FeedbackSubmissionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        public string? LastTarget { get; private set; }

        public Task<bool> LaunchAsync(
            string target,
            CancellationToken cancellationToken = default
        )
        {
            LastTarget = target;
            return Task.FromResult(true);
        }
    }
}
