namespace Tyflocentrum.Windows.UI.ViewModels;

public sealed class PlaybackRateOptionViewModel
{
    public PlaybackRateOptionViewModel(double value, string label)
    {
        Value = value;
        Label = label;
    }

    public double Value { get; }

    public string Label { get; }
}
