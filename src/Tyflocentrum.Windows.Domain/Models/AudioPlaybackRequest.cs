namespace Tyflocentrum.Windows.Domain.Models;

public sealed record AudioPlaybackRequest(
    string SourceTypeLabel,
    string Title,
    string? Subtitle,
    Uri SourceUrl,
    bool IsLive,
    bool CanSeek,
    bool CanChangePlaybackRate,
    int? PodcastPostId = null,
    double? InitialSeekSeconds = null
);
