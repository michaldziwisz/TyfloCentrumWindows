namespace TyfloCentrum.Windows.Domain.Services;

/// <summary>
/// Progresywny bufor pobierania pliku audio. Zamiast pozwalać silnikowi
/// odtwarzania (Windows Media Foundation) samodzielnie odpytywać serwer zakresami
/// "od pozycji do końca" (co powodowało wielokrotne pełne pobrania i "mulenie"
/// przy przewijaniu), aplikacja pobiera plik JEDNYM ciągłym żądaniem do pliku
/// tymczasowego, a odtwarzacz czyta z tego bufora. Odczyt zakresu jeszcze nie
/// pobranego blokuje do momentu dociągnięcia danych (lub końca pobierania).
/// </summary>
public interface IProgressiveMediaCache : IAsyncDisposable
{
    /// <summary>Całkowita długość zasobu w bajtach (znana po nagłówkach odpowiedzi).</summary>
    long TotalLength { get; }

    /// <summary>Liczba bajtów już pobranych i dostępnych do natychmiastowego odczytu.</summary>
    long AvailableLength { get; }

    /// <summary>Czy pobieranie całości zostało zakończone.</summary>
    bool IsComplete { get; }

    /// <summary>
    /// Otwiera bufor: wykonuje żądanie, ustala <see cref="TotalLength"/> i startuje
    /// pobieranie w tle. Zwraca po odczytaniu nagłówków (nie czeka na cały plik).
    /// </summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Czyta do <paramref name="count"/> bajtów od pozycji <paramref name="position"/>.
    /// Jeśli dane nie są jeszcze pobrane, czeka aż spłyną (lub pobieranie się skończy).
    /// Zwraca liczbę faktycznie odczytanych bajtów (0 = koniec pliku).
    /// </summary>
    Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        long position,
        int count,
        CancellationToken cancellationToken = default
    );
}
