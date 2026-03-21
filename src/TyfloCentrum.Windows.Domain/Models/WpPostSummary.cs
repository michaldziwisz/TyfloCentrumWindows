using System.Text.Json.Serialization;

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
}
