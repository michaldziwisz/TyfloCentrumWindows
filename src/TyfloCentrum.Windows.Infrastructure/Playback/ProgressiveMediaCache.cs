using System.Net.Http.Headers;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Playback;

/// <summary>
/// Progresywny bufor pobierania z obsługą odczytu losowego.
///
/// Model hybrydowy (rozwiązuje realny problem wykryty w logu Media Foundation):
///  - GŁÓWNE pobranie: jedno ciągłe żądanie od bajtu 0 do pliku tymczasowego.
///    To pokrywa normalne odtwarzanie od początku i daje jedno zliczenie pobrania
///    na serwerze dla właściwej treści.
///  - ODCZYT W LUCE (pozycja daleko przed sekwencyjnym frontem — np. Media
///    Foundation na starcie skacze na KONIEC pliku, by odczytać nagłówek/indeks
///    MP3, albo użytkownik przewija do przodu): zamiast blokować do ściągnięcia
///    całości, robimy OSOBNY mały range-request tylko po żądany fragment i
///    zwracamy go od razu. Ostatnia luka jest cache'owana w pamięci, więc
///    powtórne odczyty tej samej końcówki nie generują kolejnych żądań.
///
/// Dzięki temu odtwarzanie startuje natychmiast (koniec 30-sekundowej blokady na
/// ściąganie całego pliku), a liczba żądań pozostaje mała (jedno duże pobranie +
/// pojedyncze małe range'y na seeki).
/// </summary>
public sealed class ProgressiveMediaCache : IProgressiveMediaCache
{
    // Jeśli żądana pozycja mieści się w tym oknie przed sekwencyjnym frontem,
    // czekamy na sekwencyjne dociągnięcie (normalne odtwarzanie). Dalej = luka.
    private const long SequentialWindowBytes = 2L * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly Uri _sourceUri;
    private readonly string _tempFilePath;
    private readonly SemaphoreSlim _readFileLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly CancellationTokenSource _downloadCts = new();

    private TaskCompletionSource<bool> _progressSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _availableLength;
    private bool _isComplete;
    private Exception? _downloadError;
    private Task? _downloadTask;
    private bool _disposed;

    // Cache ostatniej luki (range-fetch), by powtórne odczyty tej samej końcówki
    // nie generowały kolejnych żądań HTTP.
    private long _gapCacheStart = -1;
    private byte[] _gapCacheData = [];

    public ProgressiveMediaCache(HttpClient httpClient, Uri sourceUri, string? tempFilePath = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sourceUri = sourceUri ?? throw new ArgumentNullException(nameof(sourceUri));
        _tempFilePath = string.IsNullOrWhiteSpace(tempFilePath)
            ? Path.Combine(Path.GetTempPath(), $"tc-audio-{Guid.NewGuid():N}.tmp")
            : tempFilePath;
    }

    public long TotalLength { get; private set; }

    public long AvailableLength
    {
        get
        {
            lock (_stateLock)
            {
                return _availableLength;
            }
        }
    }

    public bool IsComplete
    {
        get
        {
            lock (_stateLock)
            {
                return _isComplete;
            }
        }
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var response = await _httpClient.GetAsync(
            _sourceUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is > 0)
        {
            TotalLength = contentLength.Value;
        }
        else if (response.Content.Headers.ContentRange?.Length is long rangeLength and > 0)
        {
            TotalLength = rangeLength;
        }
        else
        {
            TotalLength = 0;
        }

        _downloadTask = Task.Run(() => DownloadLoopAsync(response), CancellationToken.None);
    }

    private async Task DownloadLoopAsync(HttpResponseMessage response)
    {
        try
        {
            using (response)
            await using (var httpStream = await response.Content.ReadAsStreamAsync(_downloadCts.Token))
            await using (var fileStream = new FileStream(
                _tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1 << 16,
                useAsync: true))
            {
                var buffer = new byte[1 << 16];
                int read;
                while ((read = await httpStream.ReadAsync(buffer, _downloadCts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), _downloadCts.Token);
                    await fileStream.FlushAsync(_downloadCts.Token);
                    AdvanceAvailable(read);
                }
            }

            MarkComplete(error: null);
        }
        catch (OperationCanceledException)
        {
            MarkComplete(error: null);
        }
        catch (Exception ex)
        {
            MarkComplete(ex);
        }
    }

    private void AdvanceAvailable(int read)
    {
        TaskCompletionSource<bool> signalToRelease;
        lock (_stateLock)
        {
            _availableLength += read;
            signalToRelease = _progressSignal;
            _progressSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
        }
        signalToRelease.TrySetResult(true);
    }

    private void MarkComplete(Exception? error)
    {
        TaskCompletionSource<bool> signalToRelease;
        lock (_stateLock)
        {
            _isComplete = true;
            _downloadError = error;
            signalToRelease = _progressSignal;
        }
        signalToRelease.TrySetResult(true);
    }

    public async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        long position,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
        if (count <= 0)
        {
            return 0;
        }

        // Poza końcem znanego pliku -> EOF.
        if (TotalLength > 0 && position >= TotalLength)
        {
            return 0;
        }

        // Decyzja: czekać na sekwencyjny front czy zrobić range-fetch luki.
        while (true)
        {
            Task waitTask;
            bool needGapFetch = false;
            lock (_stateLock)
            {
                if (_downloadError is not null)
                {
                    throw new IOException("Nie udało się pobrać pliku audio.", _downloadError);
                }

                // Dane już w sekwencyjnym froncie -> czytaj z pliku.
                if (position < _availableLength)
                {
                    break;
                }

                // Pobieranie zakończone: wszystko co jest, jest w pliku.
                if (_isComplete)
                {
                    if (position >= _availableLength)
                    {
                        return 0; // EOF
                    }
                    break;
                }

                // Pozycja tuż przed frontem (normalne odtwarzanie) -> poczekaj na
                // sekwencyjne dociągnięcie. Pozycja daleko (luka: koniec pliku,
                // przewinięcie) -> range-fetch poza lockiem.
                if (position > _availableLength + SequentialWindowBytes)
                {
                    needGapFetch = true;
                    waitTask = Task.CompletedTask;
                }
                else
                {
                    waitTask = _progressSignal.Task;
                }
            }

            if (needGapFetch)
            {
                return await ReadGapAsync(buffer, offset, position, count, cancellationToken);
            }

            await waitTask.WaitAsync(cancellationToken);
        }

        return await ReadFromFileAsync(buffer, offset, position, count, cancellationToken);
    }

    private async Task<int> ReadFromFileAsync(
        byte[] buffer,
        int offset,
        long position,
        int count,
        CancellationToken cancellationToken
    )
    {
        long available = AvailableLength;
        if (position >= available)
        {
            return 0;
        }

        var toRead = (int)Math.Min(count, available - position);
        if (toRead <= 0)
        {
            return 0;
        }

        await _readFileLock.WaitAsync(cancellationToken);
        try
        {
            await using var readStream = new FileStream(
                _tempFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 1 << 16,
                useAsync: true
            );
            readStream.Seek(position, SeekOrigin.Begin);

            var totalRead = 0;
            while (totalRead < toRead)
            {
                var chunk = await readStream.ReadAsync(
                    buffer.AsMemory(offset + totalRead, toRead - totalRead),
                    cancellationToken
                );
                if (chunk == 0)
                {
                    break;
                }
                totalRead += chunk;
            }

            return totalRead;
        }
        finally
        {
            _readFileLock.Release();
        }
    }

    /// <summary>
    /// Odczyt luki: pozycja daleko przed sekwencyjnym frontem (np. koniec pliku
    /// przy parsowaniu nagłówka MP3 albo przewinięcie w przód). Zamiast czekać na
    /// ściągnięcie całości, pobieramy tylko żądany fragment osobnym range-requestem.
    /// </summary>
    private async Task<int> ReadGapAsync(
        byte[] buffer,
        int offset,
        long position,
        int count,
        CancellationToken cancellationToken
    )
    {
        // Cache ostatniej luki (MF potrafi odczytać końcówkę kilka razy z rzędu).
        lock (_stateLock)
        {
            if (_gapCacheStart >= 0
                && position >= _gapCacheStart
                && position < _gapCacheStart + _gapCacheData.Length)
            {
                var cacheOffset = (int)(position - _gapCacheStart);
                var fromCache = Math.Min(count, _gapCacheData.Length - cacheOffset);
                Array.Copy(_gapCacheData, cacheOffset, buffer, offset, fromCache);
                return fromCache;
            }
        }

        // Pobierz z zapasem (żeby kolejne pobliskie odczyty trafiły w cache), ale
        // rozsądnie mały (nagłówek/indeks MP3 to kilka-kilkadziesiąt KB).
        var fetchLength = Math.Max(count, 256 * 1024);
        long fetchEnd = position + fetchLength - 1;
        if (TotalLength > 0 && fetchEnd > TotalLength - 1)
        {
            fetchEnd = TotalLength - 1;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, _sourceUri);
        request.Headers.Range = new RangeHeaderValue(position, fetchEnd);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (data.Length == 0)
        {
            return 0;
        }

        lock (_stateLock)
        {
            _gapCacheStart = position;
            _gapCacheData = data;
        }

        var toCopy = Math.Min(count, data.Length);
        Array.Copy(data, 0, buffer, offset, toCopy);
        return toCopy;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try
        {
            await _downloadCts.CancelAsync();
        }
        catch
        {
            // ignoruj — sprzątanie
        }

        if (_downloadTask is not null)
        {
            try
            {
                await _downloadTask;
            }
            catch
            {
                // pobieranie mogło zostać anulowane — ignoruj
            }
        }

        _downloadCts.Dispose();
        _readFileLock.Dispose();

        try
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }
        catch
        {
            // plik tymczasowy — sprzątanie best-effort
        }
    }
}
