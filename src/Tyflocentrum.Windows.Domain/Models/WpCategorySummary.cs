using System.Text.Json.Serialization;

namespace Tyflocentrum.Windows.Domain.Models;

public sealed record WpCategorySummary
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("count")]
    public required int Count { get; init; }
}
