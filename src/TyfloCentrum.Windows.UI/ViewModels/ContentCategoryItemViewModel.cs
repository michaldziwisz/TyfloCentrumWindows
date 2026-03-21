namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class ContentCategoryItemViewModel
{
    public ContentCategoryItemViewModel(int? id, string name, int? count = null)
    {
        Id = id;
        Name = name;
        Count = count;
    }

    public int? Id { get; }

    public string Name { get; }

    public int? Count { get; }

    public string CountLabel => Count is int value && value >= 0 ? value.ToString() : string.Empty;

    public string AccessibleLabel =>
        Count is int value ? $"{Name}, {value} pozycji" : Name;

    public override string ToString() => AccessibleLabel;
}
