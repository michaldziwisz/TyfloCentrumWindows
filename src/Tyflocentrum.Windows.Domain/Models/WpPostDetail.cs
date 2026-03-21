using System.Text.Json.Serialization;

namespace Tyflocentrum.Windows.Domain.Models;

public sealed record WpPostDetail
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("title")]
    public required RenderedText Title { get; init; }

    [JsonPropertyName("excerpt")]
    public RenderedText? Excerpt { get; init; }

    [JsonPropertyName("content")]
    public required RenderedText Content { get; init; }

    [JsonPropertyName("guid")]
    public RenderedText? Guid { get; init; }

    [JsonPropertyName("link")]
    public string? Link { get; init; }
}
