namespace TyfloCentrum.Windows.Domain.Models;

public sealed record PodcastShowNotesSnapshot(
    IReadOnlyList<WordPressComment> Comments,
    IReadOnlyList<ChapterMarker> Markers,
    IReadOnlyList<RelatedLink> Links,
    RelatedLink? TextVersion = null
)
{
    public bool HasComments => Comments.Count > 0;

    public bool HasChapterMarkers => Markers.Count > 0;

    public bool HasRelatedLinks => Links.Count > 0;

    public bool HasTextVersion => TextVersion is not null;
}
