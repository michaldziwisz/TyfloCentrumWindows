using System.Globalization;
using System.Text.Json;

namespace TyfloCentrum.Windows.Domain.Metadata;

public static class ContentTimePolicy
{
    public static ContentTimeValue? Audio(JsonElement? metadata)
    {
        if (Integer(Field(metadata, "schema_version")) != 1 || Text(Field(metadata, "audio_status")) != "ready") return null;
        var duration = Field(metadata, "duration_seconds");
        if (duration is not { ValueKind: JsonValueKind.Number } number || !number.TryGetDouble(out var seconds)
            || !double.IsFinite(seconds) || seconds <= 0 || seconds > int.MaxValue) return null;
        return new ContentTimeValue((int)Math.Ceiling(seconds), true);
    }

    public static ContentTimeValue? Reading(JsonElement? item, JsonElement? sourceModified, DateTimeOffset now)
    {
        var metadata = Field(item, "tyflocentrum");
        if (Text(Field(item, "freshness")) != "fresh" || Integer(Field(metadata, "schema_version")) != 1
            || Text(Field(metadata, "text_status")) != "ready") return null;
        var words = Integer(Field(metadata, "word_count"));
        var minutes = Integer(Field(metadata, "reading_minutes"));
        if (words is not > 0 || minutes is not > 0 || minutes != ((long)words.Value + 199) / 200) return null;
        var checkedAt = Date(Field(item, "checked_at"));
        if (checkedAt is null || checkedAt > now || now - checkedAt >= TimeSpan.FromHours(24)) return null;
        var source = Date(sourceModified);
        var modified = Date(Field(item, "modified_gmt"));
        if (source is not null && (modified is null || source > modified)) return null;
        return new ContentTimeValue(minutes.Value, false, checkedAt.Value.AddHours(24));
    }

    public static JsonElement? Field(JsonElement? value, string name) =>
        value is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var result) ? result : null;
    public static int? Integer(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } n && n.TryGetInt32(out var result) ? result : null;
    public static string? Text(JsonElement? value) => value is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"
    ];

    public static DateTimeOffset? Date(JsonElement? value) =>
        DateTimeOffset.TryParseExact(Text(value), DateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result) ? result : null;
}
