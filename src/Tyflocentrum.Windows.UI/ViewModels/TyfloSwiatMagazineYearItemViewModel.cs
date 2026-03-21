namespace Tyflocentrum.Windows.UI.ViewModels;

public sealed class TyfloSwiatMagazineYearItemViewModel
{
    public TyfloSwiatMagazineYearItemViewModel(int year, int count)
    {
        Year = year;
        Count = count;
    }

    public int Year { get; }

    public int Count { get; }

    public string Label => Year > 0 ? Year.ToString() : "Pozostałe";

    public string CountLabel => Count switch
    {
        1 => "1 numer",
        >= 2 and <= 4 => $"{Count} numery",
        _ => $"{Count} numerów",
    };

    public string AccessibleLabel => $"{Label}. {CountLabel}.";

    public override string ToString() => AccessibleLabel;
}
