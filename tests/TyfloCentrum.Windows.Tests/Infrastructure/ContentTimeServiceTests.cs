using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using Xunit;
using Xunit.Abstractions;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class ContentTimeServiceTests(ITestOutputHelper output)
{
    internal static readonly TyfloCentrumEndpointsOptions Options = new()
    {
        ContentMetadataUrl = new("https://metadata.example/v1/metadata"),
        TyflopodcastApiBaseUrl = new("https://podcasts.example/wp-json/"),
        TyfloswiatApiBaseUrl = new("https://articles.example/wp-json/")
    };
    internal static ContentTimeKey Key(int id, bool page = false) => new(ContentSource.Article, page, id);
    internal static JsonElement Reading(int id, DateTimeOffset? now = null, int minutes = 2) => JsonSerializer.SerializeToElement(new
    {
        id, freshness = "fresh", checked_at = now ?? DateTimeOffset.UtcNow,
        modified_gmt = "2026-10-07T10:00:00", tyflocentrum = new { schema_version = 1, text_status = "ready", word_count = minutes * 200, reading_minutes = minutes }
    });
    internal static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    internal static Dictionary<string, string> Query(Uri uri) => uri.Query.TrimStart('?').Split('&')
        .Select(s => s.Split('=', 2)).ToDictionary(a => a[0], a => Uri.UnescapeDataString(a[1]));
    internal static HttpResponseMessage Batch(Uri uri, DateTimeOffset? now = null, int minutes = 2)
    {
        var q = Query(uri);
        if (uri.Host == "podcasts.example") return Json(q["include"].Split(',').Select(id => new { id = int.Parse(id), tyflocentrum = new { schema_version = 1, audio_status = "ready", duration_seconds = 2.5 } }));
        return Json(new { schema_version = 1, source = "tyfloswiat.pl", type = q["type"], items = Enumerable.Reverse(q["ids"].Split(',')).Select(id => Reading(int.Parse(id), now, minutes)) });
    }

    [Fact]
    public async Task BatchesDeduplicateChunkAt50SeparateTypesAndMergeByKey()
    {
        using var handler = new Transport((u, _) => Task.FromResult(Batch(u)));
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options);
        var keys = Enumerable.Range(1, 121).Select(i => Key(i)).Concat([Key(42), Key(42, true), new(ContentSource.Podcast, false, 42), Key(0), Key(-1)]).ToArray();
        var result = await service.GetAsync(keys);
        Assert.Equal(123, result.Count);
        Assert.Equal(5, handler.Urls.Count);
        Assert.All(result.Where(p => p.Key.Source == ContentSource.Article), p => Assert.Equal(p.Key.Id, ContentTimePolicy.Integer(ContentTimePolicy.Field(p.Value, "id"))));
        Assert.Equal(3, ContentTimePolicy.Audio(result[new(ContentSource.Podcast, false, 42)])?.Amount);
        foreach (var uri in handler.Urls)
        {
            var q = Query(uri);
            Assert.InRange(q.GetValueOrDefault("ids", q.GetValueOrDefault("include", "")).Split(',').Length, 1, 50);
            Assert.DoesNotContain("content", uri.Query);
            Assert.DoesNotContain(".mp3", uri.AbsoluteUri);
            Assert.DoesNotContain("pobierz", uri.AbsoluteUri);
        }
        output.WriteLine("URL_REGISTER=" + JsonSerializer.Serialize(handler.Urls));
    }

    [Fact]
    public async Task DuplicateForeignAndMalformedItemsCannotPoisonOtherRows()
    {
        using var handler = new Transport((_, _) => Task.FromResult(Json(new { schema_version = 1, source = "tyfloswiat.pl", type = "posts", items = new object[] { Reading(99), Reading(2), true, new { id = 3, tyflocentrum = false }, Reading(1), Reading(1) } })));
        using var http = new HttpClient(handler);
        var values = await new ContentTimeService(http, Options).GetAsync([Key(1), Key(2), Key(3), Key(4)]);
        Assert.Equal(4, values.Count);
        Assert.Null(values[Key(1)]);
        Assert.Equal(2, ContentTimePolicy.Reading(values[Key(2)], null, DateTimeOffset.UtcNow)?.Amount);
        Assert.Null(ContentTimePolicy.Reading(values[Key(3)], null, DateTimeOffset.UtcNow));
        Assert.Null(values[Key(4)]);
    }

    [Theory]
    [InlineData("503")]
    [InlineData("429")]
    [InlineData("malformed")]
    [InlineData("timeout")]
    [InlineData("wrong_type")]
    [InlineData("wrong_schema")]
    public async Task FailuresAreOptionalAndNegativelyCachedWithoutRetry(string mode)
    {
        using var handler = new Transport(async (_, ct) =>
        {
            if (mode == "timeout") await Task.Delay(Timeout.Infinite, ct);
            if (int.TryParse(mode, out var status)) return new HttpResponseMessage((HttpStatusCode)status);
            if (mode == "malformed") return new(HttpStatusCode.OK) { Content = new StringContent("broken") };
            return Json(new { schema_version = mode == "wrong_schema" ? 2 : 1, source = "tyfloswiat.pl", type = "pages", items = new[] { Reading(1) } });
        });
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options, timeout: TimeSpan.FromMilliseconds(100));
        Assert.Null((await service.GetAsync([Key(1)]))[Key(1)]);
        Assert.Null((await service.GetAsync([Key(1)]))[Key(1)]);
        Assert.Single(handler.Urls);
    }

    [Fact]
    public async Task SharedFlightSurvivesOneSubscriberCancellation()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Transport(async (uri, ct) => { await release.Task.WaitAsync(ct); return Batch(uri); });
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options);
        using var cancel = new CancellationTokenSource();
        var first = service.GetAsync([Key(1)], cancel.Token);
        var second = service.GetAsync([Key(1)]);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult();
        Assert.NotNull((await second)[Key(1)]);
        Assert.Single(handler.Urls);
    }

    [Fact]
    public async Task CancelledOldTransportCannotReplaceFreshCache()
    {
        var old = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new Transport((uri, _) => Interlocked.Increment(ref calls) == 1 ? old.Task : Task.FromResult(Batch(uri, minutes: 7)));
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options);
        using var cancel = new CancellationTokenSource();
        var first = service.GetAsync([Key(1)], cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var fresh = await service.GetAsync([Key(1)]);
        old.SetResult(Batch(handler.Urls.First(), minutes: 2));
        Assert.Equal(7, ContentTimePolicy.Reading(fresh[Key(1)], null, DateTimeOffset.UtcNow)?.Amount);
        Assert.Equal(7, ContentTimePolicy.Reading((await service.GetAsync([Key(1)]))[Key(1)], null, DateTimeOffset.UtcNow)?.Amount);
        Assert.Equal(2, handler.Urls.Count);
    }

    [Fact]
    public async Task CacheIsBoundedAndCannotExtendFreshnessPastCheckedAt()
    {
        var clock = new Clock();
        var checkedAt = clock.GetUtcNow().AddHours(-24).AddSeconds(5);
        using var handler = new Transport((uri, _) => Task.FromResult(Batch(uri, checkedAt)));
        using var http = new HttpClient(handler);
        var service = new ContentTimeService(http, Options, clock, capacity: 2);
        var first = await service.GetAsync([Key(1)]);
        Assert.NotNull(ContentTimePolicy.Reading(first[Key(1)], null, clock.GetUtcNow()));
        clock.Now = clock.Now.AddSeconds(6);
        var expired = await service.GetAsync([Key(1)]);
        Assert.Null(ContentTimePolicy.Reading(expired[Key(1)], null, clock.GetUtcNow()));
        await service.GetAsync([Key(2), Key(3)]);
        await service.GetAsync([Key(1)]);
        Assert.Equal(4, handler.Urls.Count);
    }

    internal sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-08T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class Transport(Func<Uri, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public ConcurrentQueue<Uri> Urls { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Urls.Enqueue(request.RequestUri!);
            return respond(request.RequestUri!, cancellationToken);
        }
    }
}
