namespace TyfloCentrum.Windows.Domain.Models;

public sealed record ShowNotesParseResult(
    IReadOnlyList<ChapterMarker> Markers,
    IReadOnlyList<RelatedLink> Links
);
