using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContentTimeAcceptanceTests
{
    public static IEnumerable<object[]> AudioCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-time.json")));
        return document.RootElement.GetProperty("audio_cases").EnumerateArray()
            .Select(item => new object[] { item.GetProperty("name").GetString()!, item.GetProperty("metadata").GetRawText(), item.GetProperty("expected_accessible").GetString()! }).ToArray();
    }

    [Theory]
    [MemberData(nameof(AudioCases))]
    public void AudioMetadataReachesCatalogNewsAndFavorites(string name, string metadata, string expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        var post = JsonSerializer.Deserialize<WpPostSummary>("{\"id\":42,\"date\":\"2026-10-08T10:00:00\",\"title\":{\"rendered\":\"Audycja\"},\"link\":\"https://example.test/42\",\"tyflocentrum\":" + metadata + "}")!;
        var favorite = JsonSerializer.Deserialize<FavoriteItem>("{\"Id\":\"Podcast:42\",\"PostId\":42,\"Title\":\"Audycja\",\"tyflocentrum\":" + metadata + "}")!;
        Assert.Contains(expected, new ContentPostItemViewModel(ContentSource.Podcast, post).AccessibleLabel);
        Assert.Contains(expected, new NewsFeedItemViewModel(new NewsFeedItem(NewsItemKind.Podcast, post)).AccessibleLabel);
        Assert.Contains(expected, new FavoriteItemViewModel(favorite).AccessibleLabel);
    }
}
