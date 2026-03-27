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

        var service = new PodcastShowNotesService(commentsService);

        var result = await service.GetAsync(77);

        Assert.Equal(2, result.Comments.Count);
        Assert.Equal(2, result.Markers.Count);
        Assert.Single(result.Links);
        Assert.Equal("Wstęp", result.Markers[0].Title);
        Assert.Equal(5d, result.Markers[0].Seconds);
        Assert.Equal("Strona projektu", result.Links[0].Title);
        Assert.Equal("https://example.com/projekt", result.Links[0].Url.AbsoluteUri);
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
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(_comments);
        }
    }
}
