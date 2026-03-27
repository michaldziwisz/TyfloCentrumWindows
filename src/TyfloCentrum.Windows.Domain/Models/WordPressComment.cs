using System.Text.Json.Serialization;
using System.Globalization;

namespace TyfloCentrum.Windows.Domain.Models;

public sealed record WordPressComment
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("post")]
    public required int PostId { get; init; }

    [JsonPropertyName("parent")]
    public required int ParentId { get; init; }

    [JsonPropertyName("author_name")]
    public required string AuthorName { get; init; }

    [JsonPropertyName("date_gmt")]
    public string? DateGmt { get; init; }

    [JsonPropertyName("content")]
    public required RenderedText Content { get; init; }

    public DateTimeOffset? PublishedAtUtc => ParsePublishedAtUtc(DateGmt);

    private static DateTimeOffset? ParsePublishedAtUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (
            DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedOffset
            )
        )
        {
            return parsedOffset;
        }

        if (
            DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedDateTime
            )
        )
        {
            return new DateTimeOffset(parsedDateTime, TimeSpan.Zero);
        }

        return null;
    }
}
