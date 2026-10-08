using System.Text.Json;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Http;

/// <summary>Oddzielny, opcjonalny transport. Nigdy nie pobiera treści ani nagrań.</summary>
public sealed class ContentTimeService : IContentTimeService
{
    private readonly HttpClient _http;
    private readonly TyfloCentrumEndpointsOptions _options;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _timeout;
    private readonly int _capacity;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _parallel = new(2);
    private readonly Dictionary<ContentTimeKey, CacheEntry> _cache = [];
    private readonly Dictionary<ContentTimeKey, Flight> _flights = [];
    private long _sequence;

    public ContentTimeService(HttpClient http, TyfloCentrumEndpointsOptions options,
        TimeProvider? clock = null, TimeSpan? timeout = null, int capacity = 512)
    {
        _http = http;
        _options = options;
        _clock = clock ?? TimeProvider.System;
        _timeout = timeout ?? TimeSpan.FromSeconds(3);
        _capacity = Math.Max(1, capacity);
    }

    public async Task<IReadOnlyDictionary<ContentTimeKey, JsonElement?>> GetAsync(
        IEnumerable<ContentTimeKey> keys, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = keys.Where(k => k.IsValid).Distinct().ToArray();
        var result = new Dictionary<ContentTimeKey, JsonElement?>();
        var waits = new HashSet<Flight>();
        var starts = new List<Flight>();
        lock (_sync)
        {
            var missing = new List<ContentTimeKey>();
            foreach (var key in requested)
            {
                if (_cache.TryGetValue(key, out var entry) && entry.Until > _clock.GetUtcNow())
                {
                    result[key] = entry.Value;
                    continue;
                }
                _cache.Remove(key);
                if (_flights.TryGetValue(key, out var active) && !active.Cancellation.IsCancellationRequested)
                    waits.Add(active);
                else missing.Add(key);
            }
            foreach (var group in missing.GroupBy(k => (k.Source, k.IsPage)))
            foreach (var chunk in group.Chunk(50))
            {
                var flight = new Flight(chunk);
                foreach (var key in chunk) _flights[key] = flight;
                waits.Add(flight);
                starts.Add(flight);
            }
            foreach (var flight in waits) flight.Waiters++;
        }
        foreach (var flight in starts) _ = FetchAsync(flight);
        try
        {
            await Task.WhenAll(waits.Select(f => f.Completion.Task)).WaitAsync(cancellationToken).ConfigureAwait(false);
            foreach (var flight in waits)
            foreach (var pair in await flight.Completion.Task.ConfigureAwait(false))
                if (requested.Contains(pair.Key)) result[pair.Key] = pair.Value;
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            lock (_sync)
            {
                foreach (var flight in waits)
                {
                    flight.Waiters--;
                    if (flight.Waiters == 0 && !flight.Completion.Task.IsCompleted) flight.Cancellation.Cancel();
                }
            }
        }
    }

    private async Task FetchAsync(Flight flight)
    {
        var values = flight.Keys.ToDictionary(k => k, _ => (JsonElement?)null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(flight.Cancellation.Token);
        timeout.CancelAfter(_timeout);
        var entered = false;
        try
        {
            await _parallel.WaitAsync(timeout.Token).ConfigureAwait(false);
            entered = true;
            using var response = await SendAsync(BuildUri(flight.Keys), timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.LoadIntoBufferAsync(262144).WaitAsync(timeout.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            Parse(document.RootElement, flight.Keys, values);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            // Nie ma retry. Także awaria ma krótki cache, niezależny od cache listy.
        }
        finally
        {
            if (entered) _parallel.Release();
            lock (_sync)
            {
                foreach (var key in flight.Keys)
                {
                    if (!_flights.TryGetValue(key, out var current) || !ReferenceEquals(current, flight)) continue;
                    _flights.Remove(key);
                    if (flight.Cancellation.IsCancellationRequested) continue;
                    var now = _clock.GetUtcNow();
                    var value = key.Source == ContentSource.Podcast ? ContentTimePolicy.Audio(values[key]) :
                        ContentTimePolicy.Reading(values[key], null, now);
                    var until = now.Add(value is null ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5));
                    if (value?.ExpiresAt is DateTimeOffset expiry && expiry < until) until = expiry;
                    _cache[key] = new CacheEntry(values[key], until, ++_sequence);
                    while (_cache.Count > _capacity) _cache.Remove(_cache.MinBy(p => p.Value.Sequence).Key);
                }
                flight.Completion.TrySetResult(values);
                flight.Cancellation.Dispose();
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken token)
    {
        var pending = _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        try { return await pending.WaitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            _ = DisposeLateResponseAsync(pending);
            throw;
        }
    }

    private static async Task DisposeLateResponseAsync(Task<HttpResponseMessage> pending)
    {
        try { (await pending.ConfigureAwait(false)).Dispose(); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ObjectDisposedException) { }
    }

    private Uri BuildUri(ContentTimeKey[] keys)
    {
        var key = keys[0];
        var ids = string.Join(",", keys.Select(k => k.Id));
        if (key.Source == ContentSource.Podcast)
            return new Uri(_options.TyflopodcastApiBaseUrl, $"wp/v2/posts?context=embed&per_page=50&include={ids}&_fields=id,tyflocentrum");
        var type = key.IsPage ? "pages" : "posts";
        return new UriBuilder(_options.ContentMetadataUrl) { Query = $"source=tyfloswiat.pl&type={type}&ids={ids}" }.Uri;
    }

    private static void Parse(JsonElement root, ContentTimeKey[] keys, Dictionary<ContentTimeKey, JsonElement?> values)
    {
        var first = keys[0];
        var audio = first.Source == ContentSource.Podcast;
        JsonElement? items = root;
        if (!audio)
        {
            if (ContentTimePolicy.Integer(ContentTimePolicy.Field(root, "schema_version")) != 1 ||
                ContentTimePolicy.Text(ContentTimePolicy.Field(root, "source")) != "tyfloswiat.pl" ||
                ContentTimePolicy.Text(ContentTimePolicy.Field(root, "type")) != (first.IsPage ? "pages" : "posts")) return;
            items = ContentTimePolicy.Field(root, "items");
        }
        if (items is not { ValueKind: JsonValueKind.Array } array) return;
        var seen = new HashSet<ContentTimeKey>();
        foreach (var item in array.EnumerateArray())
        {
            var id = ContentTimePolicy.Integer(ContentTimePolicy.Field(item, "id"));
            if (id is null) continue;
            var key = first with { Id = id.Value };
            if (!values.ContainsKey(key)) continue;
            // Zduplikowany klucz jest niejednoznaczny: żadna z wersji nie wygrywa.
            if (!seen.Add(key)) { values[key] = null; continue; }
            values[key] = audio ? ContentTimePolicy.Field(item, "tyflocentrum")?.Clone() : item.Clone();
        }
    }

    private sealed record CacheEntry(JsonElement? Value, DateTimeOffset Until, long Sequence);
    private sealed class Flight(ContentTimeKey[] keys)
    {
        public ContentTimeKey[] Keys { get; } = keys;
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource<IReadOnlyDictionary<ContentTimeKey, JsonElement?>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Waiters { get; set; }
    }
}
