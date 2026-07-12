using System.Net;
using TyfloCentrum.Windows.Infrastructure.Playback;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class ProgressiveMediaCacheTests
{
    [Fact]
    public async Task Reads_full_content_after_download_completes()
    {
        var data = CreateSequentialBytes(5000);
        var handler = new StubStreamHandler(new MemoryStream(data), contentLength: data.Length);
        using var client = new HttpClient(handler);

        await using var cache = new ProgressiveMediaCache(client, new Uri("https://example.com/a.mp3"));
        await cache.OpenAsync();

        Assert.Equal(data.Length, cache.TotalLength);

        var buffer = new byte[data.Length];
        var read = await cache.ReadAsync(buffer, 0, position: 0, count: data.Length);

        Assert.Equal(data.Length, read);
        Assert.Equal(data, buffer);
    }

    [Fact]
    public async Task Read_from_middle_returns_correct_bytes()
    {
        var data = CreateSequentialBytes(4096);
        var handler = new StubStreamHandler(new MemoryStream(data), contentLength: data.Length);
        using var client = new HttpClient(handler);

        await using var cache = new ProgressiveMediaCache(client, new Uri("https://example.com/a.mp3"));
        await cache.OpenAsync();

        var buffer = new byte[100];
        var read = await cache.ReadAsync(buffer, 0, position: 2000, count: 100);

        Assert.Equal(100, read);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(data[2000 + i], buffer[i]);
        }
    }

    [Fact]
    public async Task Read_blocks_until_requested_range_is_downloaded()
    {
        var data = CreateSequentialBytes(3000);
        // Strumień, który wypuszcza dane porcjami dopiero po ręcznym zwolnieniu.
        var gated = new GatedStream(data, firstChunk: 1000);
        var handler = new StubStreamHandler(gated, contentLength: data.Length);
        using var client = new HttpClient(handler);

        await using var cache = new ProgressiveMediaCache(client, new Uri("https://example.com/a.mp3"));
        await cache.OpenAsync();

        // Odczyt pozycji 2500 musi POCZEKAĆ — na razie dostępne jest tylko ~1000 B.
        var buffer = new byte[10];
        var readTask = cache.ReadAsync(buffer, 0, position: 2500, count: 10);

        // Daj pobieraniu chwilę; odczyt nie powinien się zakończyć (dane niedostępne).
        var finishedEarly = await Task.WhenAny(readTask, Task.Delay(200)) == readTask;
        Assert.False(finishedEarly);

        // Zwolnij resztę danych -> odczyt powinien się dokończyć.
        gated.ReleaseRest();
        var read = await readTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(10, read);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(data[2500 + i], buffer[i]);
        }
    }

    [Fact]
    public async Task Read_past_end_returns_zero()
    {
        var data = CreateSequentialBytes(1000);
        var handler = new StubStreamHandler(new MemoryStream(data), contentLength: data.Length);
        using var client = new HttpClient(handler);

        await using var cache = new ProgressiveMediaCache(client, new Uri("https://example.com/a.mp3"));
        await cache.OpenAsync();

        // Poczekaj aż pobrane w całości.
        var warmup = new byte[1000];
        await cache.ReadAsync(warmup, 0, 0, 1000);

        var buffer = new byte[10];
        var read = await cache.ReadAsync(buffer, 0, position: 5000, count: 10);
        Assert.Equal(0, read);
    }

    [Fact]
    public async Task Read_surfaces_download_error()
    {
        var handler = new StubStreamHandler(new FaultyStream(), contentLength: 10_000);
        using var client = new HttpClient(handler);

        await using var cache = new ProgressiveMediaCache(client, new Uri("https://example.com/a.mp3"));
        await cache.OpenAsync();

        var buffer = new byte[100];
        await Assert.ThrowsAsync<IOException>(
            async () => await cache.ReadAsync(buffer, 0, position: 5000, count: 100)
        );
    }

    [Fact]
    public async Task Read_from_end_uses_range_without_waiting_for_full_download()
    {
        // Scenariusz z realnego logu: Media Foundation na starcie skacze na KONIEC
        // pliku (nagłówek/indeks MP3). Sekwencyjne pobieranie jest zablokowane (nie
        // wypuszcza danych), więc gdyby bufor czekał na front, zawisłby. Odczyt
        // luki musi pójść osobnym range-requestem i wrócić NATYCHMIAST.
        var data = CreateSequentialBytes(10_000_000);
        var blockedForever = new SemaphoreSlim(0, 1);
        var handler = new RangeAwareHandler(data, sequentialGate: blockedForever);
        using var client = new HttpClient(handler);

        await using var cache = new ProgressiveMediaCache(client, new Uri("https://example.com/a.mp3"));
        await cache.OpenAsync();

        // Czytaj 16 bajtów blisko końca — daleko przed frontem sekwencyjnym (który stoi).
        var buffer = new byte[16];
        var readTask = cache.ReadAsync(buffer, 0, position: data.Length - 16, count: 16);

        var read = await readTask.WaitAsync(TimeSpan.FromSeconds(5)); // NIE zawiesza

        Assert.Equal(16, read);
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(data[data.Length - 16 + i], buffer[i]);
        }

        blockedForever.Release(); // odblokuj sekwencyjne, by Dispose się dokończył
    }

    private static byte[] CreateSequentialBytes(int count)
    {
        var data = new byte[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = (byte)(i % 251);
        }
        return data;
    }

    /// <summary>
    /// Handler symulujący serwer z obsługą Range: główne pobranie (bez nagłówka
    /// Range) blokuje się na bramce (jak wolne/zawieszone łącze), a żądania Range
    /// zwracają dokładny fragment natychmiast.
    /// </summary>
    private sealed class RangeAwareHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private readonly SemaphoreSlim _sequentialGate;

        public RangeAwareHandler(byte[] data, SemaphoreSlim sequentialGate)
        {
            _data = data;
            _sequentialGate = sequentialGate;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var range = request.Headers.Range?.Ranges.FirstOrDefault();
            if (range?.From is long from)
            {
                var to = range.To ?? _data.Length - 1;
                var length = (int)(to - from + 1);
                var slice = new byte[length];
                Array.Copy(_data, from, slice, 0, length);
                var partial = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(slice),
                };
                partial.Content.Headers.ContentLength = length;
                return Task.FromResult(partial);
            }

            // Główne pobranie: nagłówki wracają od razu (jak realny serwer), ale
            // STRUMIEŃ body blokuje się na bramce (symuluje wolne/zawieszone łącze).
            var content = new StreamContent(new GatedBodyStream(_data, _sequentialGate));
            content.Headers.ContentLength = _data.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    /// <summary>Strumień body głównego pobrania: pierwsze czytanie czeka na bramkę.</summary>
    private sealed class GatedBodyStream : Stream
    {
        private readonly byte[] _data;
        private readonly SemaphoreSlim _gate;
        private int _position;
        private bool _waited;

        public GatedBodyStream(byte[] data, SemaphoreSlim gate)
        {
            _data = data;
            _gate = gate;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            if (!_waited)
            {
                await _gate.WaitAsync(cancellationToken);
                _waited = true;
            }
            if (_position >= _data.Length)
            {
                return 0;
            }
            var toRead = Math.Min(buffer.Length, _data.Length - _position);
            _data.AsMemory(_position, toRead).CopyTo(buffer);
            _position += toRead;
            return toRead;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StubStreamHandler : HttpMessageHandler
    {
        private readonly Stream _stream;
        private readonly long _contentLength;

        public StubStreamHandler(Stream stream, long contentLength)
        {
            _stream = stream;
            _contentLength = contentLength;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var content = new StreamContent(_stream);
            content.Headers.ContentLength = _contentLength;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            return Task.FromResult(response);
        }
    }

    /// <summary>Wypuszcza pierwszą porcję, resztę dopiero po ReleaseRest().</summary>
    private sealed class GatedStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _firstChunk;
        private readonly SemaphoreSlim _gate = new(0, 1);
        private int _position;
        private bool _released;

        public GatedStream(byte[] data, int firstChunk)
        {
            _data = data;
            _firstChunk = firstChunk;
        }

        public void ReleaseRest()
        {
            _released = true;
            _gate.Release();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            if (_position >= _data.Length)
            {
                return 0;
            }

            // Do firstChunk zwracamy od razu; powyżej czekamy na zwolnienie bramki.
            if (_position >= _firstChunk && !_released)
            {
                await _gate.WaitAsync(cancellationToken);
            }

            var limit = _released ? _data.Length : Math.Min(_firstChunk, _data.Length);
            var toRead = Math.Min(buffer.Length, limit - _position);
            if (toRead <= 0)
            {
                if (!_released)
                {
                    await _gate.WaitAsync(cancellationToken);
                    limit = _data.Length;
                    toRead = Math.Min(buffer.Length, limit - _position);
                }
                if (toRead <= 0)
                {
                    return 0;
                }
            }

            _data.AsMemory(_position, toRead).CopyTo(buffer);
            _position += toRead;
            return toRead;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _gate.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Rzuca błąd w trakcie czytania — symuluje zerwane pobieranie.</summary>
    private sealed class FaultyStream : Stream
    {
        private int _position;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            if (_position == 0)
            {
                _position += 500;
                var chunk = Math.Min(buffer.Length, 500);
                buffer.Span[..chunk].Clear();
                return ValueTask.FromResult(chunk);
            }

            throw new IOException("Symulowane zerwanie połączenia.");
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 10_000;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
