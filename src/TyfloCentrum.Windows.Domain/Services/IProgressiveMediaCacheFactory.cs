namespace TyfloCentrum.Windows.Domain.Services;

/// <summary>
/// Tworzy progresywny bufor pobierania (<see cref="IProgressiveMediaCache"/>) dla
/// danego adresu pliku audio. Pozwala odtwarzaczowi grać z jednego, ciągłego
/// pobrania zamiast pozwalać silnikowi systemowemu na wielokrotne pełne pobrania.
/// </summary>
public interface IProgressiveMediaCacheFactory
{
    IProgressiveMediaCache Create(Uri sourceUri);
}
