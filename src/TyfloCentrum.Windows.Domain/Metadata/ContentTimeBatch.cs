using System.Text.Json;

namespace TyfloCentrum.Windows.Domain.Metadata;

/// <summary>Null oznacza brak/wycofanie danych; niedostępny transport nie jest wycofaniem.</summary>
public sealed class ContentTimeBatch : Dictionary<ContentTimeKey, JsonElement?>
{
    public HashSet<ContentTimeKey> UnavailableKeys { get; } = [];
}
