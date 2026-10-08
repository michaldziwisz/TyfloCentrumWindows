using System.Text.Json;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.UI.Formatting;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Domain;

public sealed class ContentTimePolicyTests
{
    public static IEnumerable<object[]> Cases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-time.json")));
        return new[] { "text_cases", "audio_cases" }.SelectMany(group => doc.RootElement.GetProperty(group).EnumerateArray()
            .Select(c => new object[] { group, c.GetProperty("name").GetString()!, c.GetRawText() })).ToArray();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SharedContract(string group, string name, string json)
    {
        Assert.NotEmpty(name);
        using var doc = JsonDocument.Parse(json);
        var c = doc.RootElement;
        var now = DateTimeOffset.Parse("2026-10-08T12:00:00Z");
        var value = group == "audio_cases" ? ContentTimePolicy.Audio(c.GetProperty("metadata")) :
            ContentTimePolicy.Reading(JsonSerializer.SerializeToElement(new {
                tyflocentrum = c.GetProperty("metadata"), freshness = c.GetProperty("freshness"),
                checked_at = c.GetProperty("checked_at"), modified_gmt = c.GetProperty("metadata_modified_gmt")
            }), c.GetProperty("source_modified_gmt"), now);
        var expectedNumber = c.GetProperty(group == "audio_cases" ? "expected_seconds" : "expected_minutes");
        Assert.Equal(expectedNumber.ValueKind == JsonValueKind.Null ? (int?)null : expectedNumber.GetInt32(), value?.Amount);
        Assert.Equal(c.GetProperty("expected_visible").GetString(), ContentTimeFormatter.Visible(value));
        Assert.Equal(c.GetProperty("expected_accessible").GetString(), ContentTimeFormatter.Accessible(value));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("\"broken\"")]
    [InlineData("{\"schema_version\":true,\"audio_status\":\"ready\",\"duration_seconds\":5}")]
    [InlineData("{\"schema_version\":1,\"audio_status\":\"ready\",\"duration_seconds\":1e309}")]
    public void MalformedOptionalMetadataIsUnavailable(string json)
    {
        Assert.Null(ContentTimePolicy.Audio(JsonSerializer.Deserialize<JsonElement>(json)));
    }

    [Fact]
    public void MissingSourceModifiedIsAllowedButFutureCheckAndExpiredCacheAreNot()
    {
        var now = DateTimeOffset.Parse("2026-10-08T12:00:00Z");
        var raw = JsonSerializer.SerializeToElement(new { tyflocentrum = new { schema_version = 1, text_status = "ready", word_count = 201, reading_minutes = 2 }, freshness = "fresh", checked_at = now, modified_gmt = "2026-10-07T12:00:00" });
        Assert.Equal(2, ContentTimePolicy.Reading(raw, null, now)?.Amount);
        Assert.Null(ContentTimePolicy.Reading(raw, null, now.AddDays(1)));
        Assert.Null(ContentTimePolicy.Reading(raw, null, now.AddSeconds(-1)));
        Assert.Null(ContentTimePolicy.Reading(raw, JsonSerializer.SerializeToElement("2026-10-08T10:00:00"), now));
    }
}
