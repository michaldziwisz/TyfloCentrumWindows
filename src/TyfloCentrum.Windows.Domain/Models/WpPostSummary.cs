using System.Text.Json.Serialization;
using System.Globalization;

namespace TyfloCentrum.Windows.Domain.Models;

public sealed record WpPostSummary
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("title")]
    public required RenderedText Title { get; init; }

    [JsonPropertyName("excerpt")]
    public RenderedText? Excerpt { get; init; }

    [JsonPropertyName("link")]
    public required string Link { get; init; }

    public DateTimeOffset? PublishedAtUtc => ParsePublishedAtUtc(Date);

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
