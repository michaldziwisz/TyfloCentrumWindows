using System.Text.Json;
using System.Xml.Linq;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;
using static TyfloCentrum.Windows.Tests.Infrastructure.ContentTimeServiceTests;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContentTimeLifetimeTests
{
    [Fact]
    public async Task AlreadyDisplayedReadingTimeExpiresAndNotifiesWithoutReload()
    {
        var clock = new ManualClock();
        var row = new ContentTimeItemViewModel(Key(1), Reading(1, clock.Now.AddHours(-24).AddSeconds(1)), clock: clock);
        Assert.Equal("Czytanie: około 2 min", row.Visible);
        var enrichment = new ContentTimeEnrichment(null);
        enrichment.Start([row]);
        enrichment.Cancel(); // Nieudane odświeżenie źródła pozostawia stary wiersz.
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Accessible)) changed.TrySetResult(); };
        clock.Advance(TimeSpan.FromSeconds(1));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Czas niedostępny", row.Visible);
        Assert.Equal("Czas niedostępny", row.Accessible);
        row.StopExpiry();
    }

    [Fact]
    public async Task DiskListCachePreservesOptionalObjectsAndDoesNotExtendReadingAge()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "metadata-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var clock = new Clock();
            var raw = Reading(42, clock.Now.AddHours(-23));
            var post = ContentTimeScreenTests.Post(42) with { ReadingMetadata = raw, TimeMetadata = JsonSerializer.SerializeToElement(false) };
            var cache = new FileBackedTransientContentCache(directory);
            await cache.GetOrCreateAsync("metadata-test", TimeSpan.FromMinutes(5), _ => Task.FromResult(post));
            var second = new FileBackedTransientContentCache(directory);
            var cached = await second.GetOrCreateAsync<WpPostSummary>("metadata-test", TimeSpan.FromMinutes(5), _ => throw new InvalidOperationException("Cache nie został odczytany"));
            Assert.Equal(post.Id, cached.Id);
            Assert.NotNull(ContentTimePolicy.Reading(cached.ReadingMetadata, null, clock.Now));
            Assert.Null(ContentTimePolicy.Reading(cached.ReadingMetadata, null, clock.Now.AddHours(2)));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("NewsSectionView", "NewsItemTemplate")]
    [InlineData("ArticleSectionView", "PostItemTemplate")]
    [InlineData("PodcastSectionView", "PostItemTemplate")]
    [InlineData("SearchSectionView", "SearchItemTemplate")]
    [InlineData("FavoritesSectionView", "FavoriteItemTemplate")]
    [InlineData("TyfloSwiatMagazineView", "TocItemTemplate")]
    public void XamlUsesExistingDateAndNameWithoutExtraFocusableMetadataElement(string view, string template)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", view + ".xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var row = Assert.Single(document.Descendants().Where(e => (string?)e.Attribute(x + "Key") == template));
        var date = Assert.Single(row.Descendants().Where(e => (string?)e.Attribute("Text") == "{Binding DateAndTime}"));
        Assert.Equal("TextBlock", date.Name.LocalName);
        Assert.Equal("Raw", (string?)date.Attribute("AutomationProperties.AccessibilityView"));
        Assert.NotEqual("True", (string?)date.Attribute("IsTabStop"));
        Assert.Single(row.Descendants().Where(e => (string?)e.Attribute("AutomationProperties.Name") == "{Binding AccessibleLabel}"));
        if (view == "TyfloSwiatMagazineView")
        {
            var issue = document.Descendants().Single(e => (string?)e.Attribute(x + "Key") == "IssueItemTemplate");
            Assert.DoesNotContain(issue.Descendants(), e => (string?)e.Attribute("Text") == "{Binding DateAndTime}");
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } = DateTimeOffset.Parse("2026-10-08T12:00:00Z");
        private readonly List<Timer> _timers = [];
        public override DateTimeOffset GetUtcNow() => Now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state, Now + dueTime);
            _timers.Add(timer);
            return timer;
        }
        public void Advance(TimeSpan amount)
        {
            Now += amount;
            foreach (var timer in _timers.ToArray()) timer.Fire();
        }
        private sealed class Timer(ManualClock clock, TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            private bool _disposed;
            private DateTimeOffset _due = due;
            public bool Change(TimeSpan dueTime, TimeSpan period) { _due = clock.Now + dueTime; return !_disposed; }
            public void Fire() { if (!_disposed && clock.Now >= _due) { _disposed = true; callback(state); } }
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
