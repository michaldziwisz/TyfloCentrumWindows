// Kompilowane wyłącznie z jawnym -p:ContentTimeUiProbe=true, nigdy w zwykłym wydaniu.
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TyfloCentrum.Windows.App;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Storage;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.App.Services;

internal static class ContentTimeUiProbe
{
    private static readonly object Gate = new();
    internal static string Root => Environment.GetEnvironmentVariable("TYFLO_REFRESH_PROBE_ROOT") is { } path && Path.IsPathFullyQualified(path) && Directory.Exists(path)
        ? path : throw new InvalidOperationException("Sonda wymaga istniejącego, jawnego katalogu dowodów.");
    internal static void Configure(IServiceCollection services)
    {
        _ = Root;
        services.AddSingleton<ILocalSettingsStore, Settings>();
        services.AddSingleton<ITransientContentCache>(_ => new FileBackedTransientContentCache(Path.Combine(Root, "http-cache")));
        services.AddSingleton<IFavoritesService>(_ => new FileFavoritesService(Path.Combine(Root, "favorites.json")));
        services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = new Transport()));
        var favorites = new[] {
            new FavoriteItem { Id = "Podcast:42", Source = ContentSource.Podcast, PostId = 42, Title = "Sonda audio", PublishedDate = "08.10.2026" },
            new FavoriteItem { Id = "Article:42", Source = ContentSource.Article, PostId = 42, Title = "Sonda tekst", PublishedDate = "08.10.2026" },
            new FavoriteItem { Id = "ArticlePage:42", Source = ContentSource.Article, ArticleOrigin = FavoriteArticleOrigin.Page, PostId = 42, Title = "Sonda strona", PublishedDate = "08.10.2026" } };
        // Konstruktor działa na wątku WinUI: żadnego sync-over-async.
        File.WriteAllText(Path.Combine(Root, "favorites.json"), JsonSerializer.Serialize(favorites, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
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
                var lists = Descendants(window.Content).OfType<ListView>().Select(list => new {
                    list.Name,
                    rows = list.Items.Cast<object>().Select(row => new {
                        identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(row),
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
                File.WriteAllText(Path.Combine(Root, "snapshot.json"), JsonSerializer.Serialize(new { pid = Environment.ProcessId, lists }));
            }
            catch (IOException) { }
        };
        timer.Start();
        window.Closed += (_, _) => timer.Stop();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private sealed class Transport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var phase = int.Parse(File.ReadAllText(Path.Combine(Root, "phase.txt")).Trim());
            var uri = request.RequestUri!;
            var q = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            lock (Gate) File.AppendAllText(Path.Combine(Root, "http.jsonl"), JsonSerializer.Serialize(new { pid = Environment.ProcessId, phase, uri = uri.AbsoluteUri, noCache = request.Headers.CacheControl?.NoCache, utc = DateTimeOffset.UtcNow }) + "\n");
            object? audio = phase == 0 ? null : phase == -2 ? false : new { schema_version = 1, audio_status = "ready", duration_seconds = phase };
            object data;
            if (uri.AbsolutePath == "/v1/metadata")
                data = new { schema_version = 1, source = "tyfloswiat.pl", type = q["type"], items = phase <= 0 ? Array.Empty<object>() : q["ids"].Split(',').Select(id => (object)new { id = int.Parse(id), freshness = "fresh", checked_at = DateTimeOffset.UtcNow, modified_gmt = "2026-10-07T10:00:00", tyflocentrum = new { schema_version = 1, text_status = "ready", word_count = phase * 200, reading_minutes = phase } }).ToArray() };
            else if (q.TryGetValue("include", out var include))
                data = include.Split(',').Select(id => new { id = int.Parse(id), tyflocentrum = audio }).ToArray();
            else if (uri.AbsolutePath.EndsWith("categories", StringComparison.Ordinal))
                data = new[] { new { id = 9, name = "Kategoria sondy", count = 1 } };
            else if (uri.AbsolutePath.EndsWith("/pages/70", StringComparison.Ordinal))
                data = new { id = 70, date = "2026-10-08T10:00:00", title = new { rendered = "Tyfloświat 1/2026" }, link = "https://tyfloswiat.pl/70", content = new { rendered = "<a href='https://tyfloswiat.pl/42'>Sonda strona</a>" } };
            else if (uri.AbsolutePath.EndsWith("/pages", StringComparison.Ordinal) && q.ContainsKey("parent"))
                data = new[] { new { id = q["parent"] == "70" ? 42 : 70, date = "2026-10-08T10:00:00", title = new { rendered = q["parent"] == "70" ? "Sonda strona" : "Tyfloświat 1/2026" }, link = q["parent"] == "70" ? "https://tyfloswiat.pl/42" : "https://tyfloswiat.pl/70" } };
            else
                data = new[] { new { id = 42, date = "2026-10-08T10:00:00", modified_gmt = "2026-10-07T10:00:00", title = new { rendered = uri.Host.Contains("tyflopodcast", StringComparison.Ordinal) ? "Sonda audio" : "Sonda tekst" }, link = "https://tyfloswiat.pl/42", tyflocentrum = audio } };
            if (phase == -1 && (uri.AbsolutePath == "/v1/metadata" || q.ContainsKey("include"))) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data)) };
            response.Headers.Add("X-WP-TotalPages", "1");
            return Task.FromResult(response);
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
