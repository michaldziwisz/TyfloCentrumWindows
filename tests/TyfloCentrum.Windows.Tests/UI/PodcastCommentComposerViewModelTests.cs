using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class PodcastCommentComposerViewModelTests
{
    [Fact]
    public async Task SubmitAsync_returns_validation_error_for_missing_required_fields()
    {
        var viewModel = new PodcastCommentComposerViewModel(
            new StubWordPressCommentsService(),
            new StubLocalSettingsStore()
        );
        viewModel.Initialize(77);

        var result = await viewModel.SubmitAsync();

        Assert.False(result.Accepted);
        Assert.Equal(PodcastCommentFormField.AuthorName, result.FocusTarget);
        Assert.Equal("Pole Imię jest obowiązkowe.", result.Message);
    }

    [Fact]
    public async Task SubmitAsync_clears_body_and_reply_target_after_successful_submission()
    {
        var commentsService = new StubWordPressCommentsService
        {
            SubmitResult = new WordPressCommentSubmissionResult(
                true,
                WordPressCommentSubmissionOutcome.PendingModeration,
                "Komentarz został przekazany do moderacji."
            ),
        };
        var viewModel = new PodcastCommentComposerViewModel(
            commentsService,
            new StubLocalSettingsStore()
        );
        viewModel.Initialize(77);
        viewModel.AuthorName = "Jan";
        viewModel.AuthorEmail = "jan@example.com";
        viewModel.Content = "Treść komentarza";
        viewModel.BeginReply(
            new CommentItemViewModel(
                new WordPressComment
                {
                    Id = 1001,
                    PostId = 77,
                    ParentId = 0,
                    AuthorName = "Autor",
                    Content = new RenderedText("<p>Treść</p>"),
                }
            )
        );

        var result = await viewModel.SubmitAsync();

        Assert.True(result.Accepted);
        Assert.Equal("Komentarz został przekazany do moderacji.", result.Message);
        Assert.Equal(string.Empty, viewModel.Content);
        Assert.False(viewModel.IsReplyMode);
        Assert.Equal("Jan", commentsService.LastRequest!.AuthorName);
        Assert.Equal("jan@example.com", commentsService.LastRequest.AuthorEmail);
        Assert.Equal(1001, commentsService.LastRequest.ParentId);
    }

    private sealed class StubWordPressCommentsService : IWordPressCommentsService
    {
        public WordPressCommentSubmissionRequest? LastRequest { get; private set; }

        public WordPressCommentSubmissionResult SubmitResult { get; set; } =
            new(
                true,
                WordPressCommentSubmissionOutcome.Published,
                "Komentarz został opublikowany."
            );

        public Task<IReadOnlyList<WordPressComment>> GetCommentsAsync(
            int postId,
            CancellationToken cancellationToken = default,
            bool forceRefresh = false
        )
        {
            return Task.FromResult((IReadOnlyList<WordPressComment>)[]);
        }

        public Task<WordPressCommentSubmissionResult> SubmitCommentAsync(
            WordPressCommentSubmissionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastRequest = request;
            return Task.FromResult(SubmitResult);
        }
    }

    private sealed class StubLocalSettingsStore : ILocalSettingsStore
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

        public ValueTask<string?> GetStringAsync(
            string key,
            CancellationToken cancellationToken = default
        )
        {
            _values.TryGetValue(key, out var value);
            return ValueTask.FromResult(value);
        }

        public ValueTask SetStringAsync(
            string key,
            string value,
            CancellationToken cancellationToken = default
        )
        {
            _values[key] = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteStringAsync(
            string key,
            CancellationToken cancellationToken = default
        )
        {
            _values.Remove(key);
            return ValueTask.CompletedTask;
        }
    }
}
