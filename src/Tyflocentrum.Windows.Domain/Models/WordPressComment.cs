using System.Text.Json.Serialization;

namespace Tyflocentrum.Windows.Domain.Models;

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

    [JsonPropertyName("content")]
    public required RenderedText Content { get; init; }
}
