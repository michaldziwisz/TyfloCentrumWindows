using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class PodcastShowNotesService : IPodcastShowNotesService
{
    private readonly IWordPressCommentsService _wordPressCommentsService;

    public PodcastShowNotesService(IWordPressCommentsService wordPressCommentsService)
    {
        _wordPressCommentsService = wordPressCommentsService;
    }

    public async Task<PodcastShowNotesSnapshot> GetAsync(
        int postId,
        CancellationToken cancellationToken = default
    )
    {
        var comments = await _wordPressCommentsService.GetCommentsAsync(postId, cancellationToken);
        var parsed = ShowNotesParser.Parse(comments);

        return new PodcastShowNotesSnapshot(comments, parsed.Markers, parsed.Links);
    }
}
