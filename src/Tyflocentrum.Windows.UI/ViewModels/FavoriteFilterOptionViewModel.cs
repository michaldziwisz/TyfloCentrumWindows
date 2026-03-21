using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.UI.ViewModels;

public sealed class FavoriteFilterOptionViewModel
{
    public FavoriteFilterOptionViewModel(string key, string label, FavoriteKind? kind)
    {
        Key = key;
        Label = label;
        Kind = kind;
    }

    public string Key { get; }

    public string Label { get; }

    public FavoriteKind? Kind { get; }

    public override string ToString() => Label;
}
