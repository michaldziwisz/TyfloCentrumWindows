using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class PodcastShowNotesService : IPodcastShowNotesService
{
    private readonly IWordPressPostDetailsService _postDetailsService;
    private readonly IWordPressCommentsService _wordPressCommentsService;

    public PodcastShowNotesService(
        IWordPressCommentsService wordPressCommentsService,
        IWordPressPostDetailsService postDetailsService
    )
    {
        _wordPressCommentsService = wordPressCommentsService;
        _postDetailsService = postDetailsService;
    }

    public async Task<PodcastShowNotesSnapshot> GetAsync(
        int postId,
        CancellationToken cancellationToken = default
    )
    {
        var comments = await _wordPressCommentsService.GetCommentsAsync(postId, cancellationToken);
        var parsed = ShowNotesParser.Parse(comments);
        var textVersion = await TryGetTextVersionLinkAsync(postId, cancellationToken);

        return new PodcastShowNotesSnapshot(comments, parsed.Markers, parsed.Links, textVersion);
    }

    private async Task<RelatedLink?> TryGetTextVersionLinkAsync(
        int postId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var post = await _postDetailsService.GetPostAsync(
                ContentSource.Podcast,
                postId,
                cancellationToken
            );
            var baseUri = Uri.TryCreate(post.Link, UriKind.Absolute, out var link) ? link : null;
            return ShowNotesParser.ParseTextVersionLink(post.Content.Rendered, baseUri);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
