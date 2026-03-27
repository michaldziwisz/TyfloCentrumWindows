namespace TyfloCentrum.Windows.Domain.Models;

public sealed record PodcastShowNotesSnapshot(
    IReadOnlyList<WordPressComment> Comments,
    IReadOnlyList<ChapterMarker> Markers,
    IReadOnlyList<RelatedLink> Links
)
{
    public bool HasComments => Comments.Count > 0;

    public bool HasChapterMarkers => Markers.Count > 0;

    public bool HasRelatedLinks => Links.Count > 0;
}
