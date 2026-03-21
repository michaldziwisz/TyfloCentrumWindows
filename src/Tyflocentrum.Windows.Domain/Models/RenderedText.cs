using System.Text.Json.Serialization;

namespace Tyflocentrum.Windows.Domain.Models;

public sealed record RenderedText(
    [property: JsonPropertyName("rendered")]
    string Rendered
);
