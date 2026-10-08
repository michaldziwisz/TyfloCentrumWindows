using TyfloCentrum.Windows.Domain.Metadata;

namespace TyfloCentrum.Windows.UI.Formatting;

public static class ContentTimeFormatter
{
    public const string Unavailable = "Czas niedostępny";
    public static string Visible(ContentTimeValue? value) => Format(value, false);
    public static string Accessible(ContentTimeValue? value) => Format(value, true);

    private static string Format(ContentTimeValue? value, bool accessible)
    {
        if (value is null) return Unavailable;
        if (!value.IsAudio) return $"Czytanie: około {value.Amount} " +
            (accessible ? (value.Amount == 1 ? "minuty" : "minut") : "min");
        var parts = new List<string>();
        Add(value.Amount / 3600, "godz.", "godzina", "godziny", "godzin");
        Add(value.Amount % 3600 / 60, "min", "minuta", "minuty", "minut");
        Add(value.Amount % 60, "s", "sekunda", "sekundy", "sekund");
        return "Czas trwania: " + string.Join(" ", parts);

        void Add(int count, string shortUnit, string one, string few, string many)
        {
            if (count == 0) return;
            var unit = !accessible ? shortUnit : count == 1 ? one :
                count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) ? few : many;
            parts.Add($"{count} {unit}");
        }
    }
}
