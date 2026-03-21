using System.Text.Json.Serialization;

namespace TyfloCentrum.Windows.Domain.Models;

public sealed record RenderedText(
    [property: JsonPropertyName("rendered")]
    string Rendered
);
