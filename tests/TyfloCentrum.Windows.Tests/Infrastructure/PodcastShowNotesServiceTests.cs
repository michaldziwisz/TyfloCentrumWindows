using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class PodcastShowNotesServiceTests
{
    [Fact]
    public async Task GetAsync_returns_comments_markers_and_related_links_for_podcast()
    {
        var commentsService = new StubWordPressCommentsService(
            [
                new WordPressComment
                {
                    Id = 1,
                    PostId = 77,
                    ParentId = 0,
                    AuthorName = "Słuchacz",
                    DateGmt = "2026-03-27T10:00:00",
                    Content = new RenderedText(
                        """
                        <p>Komentarz 1</p>
                        <p>Znaczniki czasu</p>
                        <p>Wstęp 00:05</p>
                        <p>Rozmowa 01:20</p>
                        <p>Odnośniki</p>
                        <p>- Strona projektu https://example.com/projekt</p>
                        """
                    ),
                },
                new WordPressComment
                {
                    Id = 2,
                    PostId = 77,
                    ParentId = 0,
                    AuthorName = "Drugi słuchacz",
                    DateGmt = "2026-03-27T11:00:00",
                    Content = new RenderedText("<p>Dodatkowy komentarz</p>"),
                },
            ]
        );
        var postDetailsService = new StubWordPressPostDetailsService(
            new WpPostDetail
            {
                Id = 77,
                Date = "2026-03-27T12:00:00",
                Link = "https://tyflopodcast.example/podcast-testowy/",
                Title = new RenderedText("Podcast testowy"),
                Content = new RenderedText(
                    """
                    <p>Audycja dostępna jest również w
                    <a href="https://tyflopodcast.example/tekstowe-wersje-audycji/podcast-testowy-wersja-tekstowa/">wygenerowanej automatycznie wersji tekstowej</a>.</p>
                    """
                ),
            }
        );

        var service = new PodcastShowNotesService(commentsService, postDetailsService);

        var result = await service.GetAsync(77);

        Assert.Equal(2, result.Comments.Count);
        Assert.Equal(2, result.Markers.Count);
        Assert.Single(result.Links);
        Assert.Equal("Wstęp", result.Markers[0].Title);
        Assert.Equal(5d, result.Markers[0].Seconds);
        Assert.Equal("Strona projektu", result.Links[0].Title);
        Assert.Equal("https://example.com/projekt", result.Links[0].Url.AbsoluteUri);
        Assert.NotNull(result.TextVersion);
        Assert.Equal("Tekstowa wersja odcinka", result.TextVersion!.Title);
        Assert.Equal(
            "https://tyflopodcast.example/tekstowe-wersje-audycji/podcast-testowy-wersja-tekstowa/",
            result.TextVersion.Url.AbsoluteUri
        );
    }

    private sealed class StubWordPressCommentsService : IWordPressCommentsService
    {
        private readonly IReadOnlyList<WordPressComment> _comments;

        public StubWordPressCommentsService(IReadOnlyList<WordPressComment> comments)
        {
            _comments = comments;
        }

        public Task<IReadOnlyList<WordPressComment>> GetCommentsAsync(
            int postId,
            CancellationToken cancellationToken = default,
            bool forceRefresh = false
        )
        {
            return Task.FromResult(_comments);
        }

        public Task<WordPressCommentSubmissionResult> SubmitCommentAsync(
            WordPressCommentSubmissionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubWordPressPostDetailsService : IWordPressPostDetailsService
    {
        private readonly WpPostDetail _post;

        public StubWordPressPostDetailsService(WpPostDetail post)
        {
            _post = post;
        }

        public Task<WpPostDetail> GetPostAsync(
            ContentSource source,
            int postId,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(_post);
        }
    }
}
