using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Metadata;

public readonly record struct ContentTimeKey(ContentSource Source, bool IsPage, int Id)
{
    public bool IsValid => Id > 0 && (Source == ContentSource.Article || (Source == ContentSource.Podcast && !IsPage));
}

public sealed record ContentTimeValue(int Amount, bool IsAudio, DateTimeOffset? ExpiresAt = null);
