using System.Runtime.InteropServices.WindowsRuntime;
using TyfloCentrum.Windows.Domain.Services;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace TyfloCentrum.Windows.App.Playback;

/// <summary>
/// Adapter WinRT spinający <see cref="IProgressiveMediaCache"/> z API strumienia,
/// którego oczekuje <c>MediaSource.CreateFromStream</c>. Dzięki temu Windows Media
/// Foundation czyta audio z JEDNEGO progresywnego bufora (jedno pobranie z serwera),
/// a przewijanie = zmiana pozycji w tym buforze, a NIE nowe pełne pobranie pliku.
///
/// Uwaga: MediaFoundation przy seekach woła <see cref="GetInputStreamAt"/> /
/// ustawia <see cref="Position"/>; obie ścieżki tylko przesuwają kursor w tym samym
/// współdzielonym buforze (żaden nowy transfer HTTP nie startuje).
/// </summary>
public sealed class ProgressiveRandomAccessStream : IRandomAccessStream
{
    private readonly IProgressiveMediaCache _cache;
    private readonly string _contentType;
    private ulong _position;

    public ProgressiveRandomAccessStream(IProgressiveMediaCache cache, string contentType)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _contentType = string.IsNullOrWhiteSpace(contentType) ? "audio/mpeg" : contentType;
    }

    public string ContentType => _contentType;

    public bool CanRead => true;

    public bool CanWrite => false;

    public ulong Size
    {
        get => (ulong)Math.Max(0, _cache.TotalLength);
        set => throw new NotSupportedException();
    }

    public ulong Position => _position;

    public IInputStream GetInputStreamAt(ulong position)
    {
        // Seek: przesuwamy kursor w tym samym buforze. Zwracamy widok tej samej
        // instancji (nie nowe źródło) — to klucz, by seek nie wywoływał pobrania.
        _position = position;
        return this;
    }

    public IOutputStream GetOutputStreamAt(ulong position) => throw new NotSupportedException();

    public void Seek(ulong position)
    {
        _position = position;
    }

    public IRandomAccessStream CloneStream()
    {
        // MediaFoundation potrafi klonować dla równoległych odczytów; współdzielimy
        // ten sam bufor, ale klon ma własny kursor.
        return new ProgressiveRandomAccessStream(_cache, _contentType);
    }

    public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(
        IBuffer buffer,
        uint count,
        InputStreamOptions options
    )
    {
        return AsyncInfo.Run<IBuffer, uint>(async (token, _) =>
        {
            var startPosition = (long)_position;
            var toRead = (int)Math.Min(count, buffer.Capacity);
            var managed = new byte[toRead];

            var read = await _cache.ReadAsync(
                managed,
                offset: 0,
                position: startPosition,
                count: toRead,
                cancellationToken: token
            );

            _position = (ulong)(startPosition + read);

            // Kontrakt WinRT: wypełnij PRZEKAZANY buffer i zwróć go z ustawionym
            // Length = liczba faktycznie odczytanych bajtów. Zwrócenie innego,
            // własnego bufora bywa źle obsługiwane przez Media Foundation
            // (skutek: odtwarzanie w ogóle nie startuje).
            if (read > 0)
            {
                managed.AsBuffer(0, read).CopyTo(buffer);
            }
            buffer.Length = (uint)read;
            return buffer;
        });
    }

    public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) =>
        throw new NotSupportedException();

    public IAsyncOperation<bool> FlushAsync() => throw new NotSupportedException();

    public void Dispose()
    {
        // Celowo NIE utylizujemy tu bufora (_cache): jest współdzielony między
        // klonami strumienia, a MediaFoundation tworzy i zwalnia klony w trakcie
        // odtwarzania. Właścicielem cyklu życia bufora jest odtwarzacz
        // (AudioPlayerView), który wywoła DisposeAsync bufora przy zamknięciu/zmianie
        // ścieżki. Utylizacja tutaj zabiłaby wspólny bufor po pierwszym klonie.
    }
}
