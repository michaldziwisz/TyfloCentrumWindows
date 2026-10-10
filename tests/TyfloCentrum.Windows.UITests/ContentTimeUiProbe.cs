// Kompilowane wyłącznie z jawnym -p:ContentTimeUiProbe=true, nigdy w zwykłym wydaniu.
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TyfloCentrum.Windows.App;
using TyfloCentrum.Windows.App.Views;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Storage;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.Media.Playback;

namespace TyfloCentrum.Windows.App.Services;

internal static class ContentTimeUiProbe
{
    private static readonly object Gate = new();
    private const int Count = 80;
    private static readonly List<WeakReference<AudioPlayerView>> Players = [];
    private static readonly HashSet<MediaPlayer> ObservedPlayers = [];
    internal static string Root => Environment.GetEnvironmentVariable("TYFLO_REFRESH_PROBE_ROOT") is { } path && Path.IsPathFullyQualified(path) && Directory.Exists(path)
        ? path : throw new InvalidOperationException("Sonda wymaga istniejącego, jawnego katalogu dowodów.");
    private static void Log(string file, object value)
    {
        lock (Gate) File.AppendAllText(Path.Combine(Root, file), JsonSerializer.Serialize(value) + "\n");
    }
    internal static void Configure(IServiceCollection services)
    {
        _ = Root;
        services.AddSingleton<ILocalSettingsStore, Settings>();
        services.AddSingleton<ITransientContentCache>(_ => new FileBackedTransientContentCache(Path.Combine(Root, "http-cache")));
        services.AddSingleton<IFavoritesService>(_ => new FileFavoritesService(Path.Combine(Root, "favorites.json")));
        // Ten sam rzeczywisty odtwarzacz, tylko słaba referencja do odczytowego pomiaru.
        services.AddTransient(provider => {
            var view = ActivatorUtilities.CreateInstance<AudioPlayerView>(provider);
            Players.Add(new WeakReference<AudioPlayerView>(view));
            return view;
        });
        services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = new Transport()));
        var favorites = Enumerable.Range(42, Count).SelectMany(id => new[] {
            new FavoriteItem { Id = $"Podcast:{id}", Source = ContentSource.Podcast, PostId = id, Title = $"Sonda audio {id}", PublishedDate = "08.10.2026" },
            new FavoriteItem { Id = $"Article:{id}", Source = ContentSource.Article, PostId = id, Title = $"Sonda tekst {id}", PublishedDate = "08.10.2026" },
            new FavoriteItem { Id = $"ArticlePage:{id}", Source = ContentSource.Article, ArticleOrigin = FavoriteArticleOrigin.Page, PostId = id, Title = $"Sonda strona {id}", PublishedDate = "08.10.2026" } });
        File.WriteAllText(Path.Combine(Root, "favorites.json"), JsonSerializer.Serialize(favorites, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Log("lifecycle.jsonl", new { utc = DateTimeOffset.UtcNow, host = Environment.MachineName, pid = Environment.ProcessId, started = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(), count = Count });
    }
    private static object? Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);
    private static object? Property(object instance, string name) => instance.GetType().GetProperty(name)?.GetValue(instance);
    private static int Identity(object? instance) => instance is null ? 0 : RuntimeHelpers.GetHashCode(instance);
    private static long NativeIdentity(object? instance) => instance is WinRT.IWinRTObject native ? native.NativeObject.ThisPtr.ToInt64() : 0;
    private static object PlayerSnapshot(AudioPlayerView view)
    {
        var player = Field(view, "_mediaPlayer") as MediaPlayer;
        if (player is not null && ObservedPlayers.Add(player))
        {
            player.MediaOpened += (_, _) => Log("player-events.jsonl", new { utc = DateTimeOffset.UtcNow, kind = "opened", player = Identity(player) });
            player.MediaEnded += (_, _) => Log("player-events.jsonl", new { utc = DateTimeOffset.UtcNow, kind = "ended", player = Identity(player) });
            player.MediaFailed += (_, e) => Log("player-events.jsonl", new { utc = DateTimeOffset.UtcNow, kind = "failed", message = e.ErrorMessage, player = Identity(player) });
            player.SourceChanged += (_, _) => Log("player-events.jsonl", new { utc = DateTimeOffset.UtcNow, kind = "source", player = Identity(player) });
            player.PlaybackSession.PlaybackStateChanged += (session, _) => Log("player-events.jsonl", new { utc = DateTimeOffset.UtcNow, kind = "state", state = session.PlaybackState.ToString(), player = Identity(player) });
        }
        return new { view = Identity(view), player = Identity(player), source = Identity(player?.Source), nativeSource = NativeIdentity(player?.Source), nativePlayer = NativeIdentity(player), state = player?.PlaybackSession.PlaybackState.ToString(), position = player?.PlaybackSession.Position.TotalSeconds, duration = player?.PlaybackSession.NaturalDuration.TotalSeconds, request = Field(view, "_currentRequest") };
    }
    internal static void Attach(MainWindow window)
    {
        window.Title = "TyfloCentrum: sonda odświeżania";
        window.EnsureMaximized();
        var timer = window.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) =>
        {
            try
            {
                var lists = ListViews(window.Content).Select(list => new {
                    list.Name,
                    identity = Identity(list),
                    scroll = ScrollOwners(list).Select(s => new { s.Name, s.VerticalOffset, s.ScrollableHeight, s.ViewportHeight }).ToArray(),
                    rows = list.Items.Cast<object>().Select(row => new {
                        identity = Identity(row),
                        key = Property(row, "Id") ?? Property(row, "PostId") ?? Property(row, "Key"),
                        label = row.ToString(),
                        visible = row switch {
                            ContentPostItemViewModel r => r.DateAndTime,
                            NewsFeedItemViewModel r => r.DateAndTime,
                            FavoriteItemViewModel r => r.DateAndTime,
                            TyfloSwiatMagazineTocItemViewModel r => r.DateAndTime,
                            _ => string.Empty
                        }
                    }).ToArray()
                }).ToArray();
                var players = Players.Select(reference => reference.TryGetTarget(out var view) ? PlayerSnapshot(view) : null).Where(p => p is not null).ToArray();
                var data = new { utc = DateTimeOffset.UtcNow, pid = Environment.ProcessId, lists, players };
                var path = Path.Combine(Root, "snapshot.json");
                File.WriteAllText(path + ".new", JsonSerializer.Serialize(data));
                File.Move(path + ".new", path, true);
                if (players.Length > 0) Log("playback.jsonl", new { data.utc, data.pid, players });
            }
            catch (IOException) { }
            catch (Exception e) { Log("probe-errors.jsonl", new { utc = DateTimeOffset.UtcNow, error = e.ToString() }); }
        };
        timer.Start();
        window.Closed += (_, _) => timer.Stop();
    }
    private static IEnumerable<ListView> ListViews(DependencyObject root)
    {
        if (root is ListView list) { yield return list; yield break; }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in ListViews(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static IEnumerable<ScrollViewer> ScrollOwners(ListView list)
    {
        var inner = Descendants(list).OfType<ScrollViewer>().FirstOrDefault();
        if (inner is not null) yield return inner;
        for (var parent = VisualTreeHelper.GetParent(list); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ScrollViewer scroll) yield return scroll;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private sealed class Transport : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var phase = int.Parse(File.ReadAllText(Path.Combine(Root, "phase.txt")).Trim());
            var uri = request.RequestUri!;
            var q = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            Log("http.jsonl", new { pid = Environment.ProcessId, phase, uri = uri.AbsoluteUri, method = request.Method.ToString(), noCache = request.Headers.CacheControl?.NoCache, utc = DateTimeOffset.UtcNow, range = request.Headers.Range?.ToString() });
            // Lokalny materiał przechodzi przez prawdziwy ProgressiveMediaCache i Media Foundation.
            if (uri.AbsolutePath.EndsWith("pobierz.php", StringComparison.Ordinal))
            {
                var bytes = await File.ReadAllBytesAsync(Path.Combine(Root, "fixture.mp3"), cancellationToken);
                var range = request.Headers.Range?.Ranges.FirstOrDefault();
                var start = (int)(range?.From ?? 0);
                var end = (int)(range?.To ?? bytes.Length - 1);
                var result = new HttpResponseMessage(range is null ? HttpStatusCode.OK : HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes, start, end - start + 1) };
                result.Content.Headers.ContentType = new("audio/mpeg");
                result.Headers.AcceptRanges.Add("bytes");
                if (range is not null) result.Content.Headers.ContentRange = new(start, end, bytes.Length);
                return result;
            }
            var metadata = uri.AbsolutePath == "/v1/metadata" || q.ContainsKey("include");
            if (metadata) await Task.Delay(File.Exists(Path.Combine(Root, "delay.txt")) ? int.Parse(File.ReadAllText(Path.Combine(Root, "delay.txt"))) : 100, cancellationToken);
            object? audio = phase == 0 ? null : phase == -2 ? false : new { schema_version = 1, audio_status = "ready", duration_seconds = phase };
            bool podcast = uri.Host.Contains("tyflopodcast", StringComparison.Ordinal);
            object Post(int id, string title) => new { id, date = "2026-10-08T10:00:00", modified_gmt = "2026-10-07T10:00:00", title = new { rendered = title }, link = $"https://{uri.Host}/{id}", content = new { rendered = "<p>Lokalny materiał sondy.</p>" }, excerpt = new { rendered = "Lokalna próba." }, tyflocentrum = audio };
            object data;
            int totalPages = 1;
            if (uri.AbsolutePath == "/v1/metadata")
                data = new { schema_version = 1, source = "tyfloswiat.pl", type = q["type"], items = phase <= 0 ? Array.Empty<object>() : q["ids"].Split(',').Select(id => (object)new { id = int.Parse(id), freshness = "fresh", checked_at = DateTimeOffset.UtcNow, modified_gmt = "2026-10-07T10:00:00", tyflocentrum = new { schema_version = 1, text_status = "ready", word_count = phase * 200, reading_minutes = phase } }).ToArray() };
            else if (q.TryGetValue("include", out var include))
                data = include.Split(',').Select(id => new { id = int.Parse(id), tyflocentrum = audio }).ToArray();
            else if (uri.AbsolutePath.EndsWith("categories", StringComparison.Ordinal))
                data = new[] { new { id = 9, name = "Kategoria sondy", count = Count } };
            else if (uri.AbsolutePath.EndsWith("/pages/700", StringComparison.Ordinal))
                data = new { id = 700, date = "2026-10-08T10:00:00", title = new { rendered = "Tyfloświat 1/2026" }, link = "https://tyfloswiat.pl/700", content = new { rendered = string.Join("", Enumerable.Range(42, Count).Select(id => $"<a href='https://tyfloswiat.pl/{id}'>Sonda strona {id}</a>")) } };
            else if (uri.AbsolutePath.EndsWith("/pages", StringComparison.Ordinal) && q.ContainsKey("parent"))
                data = q["parent"] == "700" ? Enumerable.Range(42, Count).Select(id => Post(id, $"Sonda strona {id}")).ToArray() : new[] { Post(700, "Tyfloświat 1/2026") };
            else if (uri.AbsolutePath.EndsWith("/posts", StringComparison.Ordinal))
            {
                int page = int.Parse(q.GetValueOrDefault("page", "1")), size = int.Parse(q.GetValueOrDefault("per_page", "20"));
                totalPages = (int)Math.Ceiling((double)Count / size);
                data = Enumerable.Range(42, Count).Skip((page - 1) * size).Take(size).Select(id => Post(id, $"Sonda {(podcast ? "audio" : "tekst")} {id}")).ToArray();
            }
            else if (uri.AbsolutePath.Contains("/posts/", StringComparison.Ordinal) && int.TryParse(uri.Segments.Last(), out var postId)) data = Post(postId, $"Sonda audio {postId}");
            else if (uri.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal)) data = Array.Empty<object>();
            else { Log("unhandled.jsonl", new { uri = uri.AbsoluteUri }); return new HttpResponseMessage(HttpStatusCode.NotFound); }
            if (phase == -1 && metadata) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data)) };
            response.Headers.Add("X-WP-TotalPages", totalPages.ToString());
            return response;
        }
    }
    private sealed class Settings : ILocalSettingsStore
    {
        private readonly Dictionary<string, string> _values = [];
        public ValueTask<string?> GetStringAsync(string key, CancellationToken cancellationToken = default) => ValueTask.FromResult(_values.GetValueOrDefault(key));
        public ValueTask SetStringAsync(string key, string value, CancellationToken cancellationToken = default) { _values[key] = value; return ValueTask.CompletedTask; }
        public ValueTask DeleteStringAsync(string key, CancellationToken cancellationToken = default) { _values.Remove(key); return ValueTask.CompletedTask; }
    }
}
