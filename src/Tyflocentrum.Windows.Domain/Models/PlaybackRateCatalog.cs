namespace Tyflocentrum.Windows.Domain.Models;

public static class PlaybackRateCatalog
{
    private static readonly double[] ValuesInternal = [1.0, 1.25, 1.5, 1.75, 2.0, 2.25, 2.5, 2.75, 3.0];

    public static IReadOnlyList<double> SupportedValues { get; } = ValuesInternal;

    public static double DefaultValue => 1.0;

    public static double Coerce(double value)
    {
        if (value <= 0)
        {
            return DefaultValue;
        }

        return ValuesInternal
            .OrderBy(candidate => Math.Abs(candidate - value))
            .ThenBy(candidate => candidate)
            .First();
    }

    public static string FormatLabel(double value)
    {
        return $"{Coerce(value):0.##}x".Replace('.', ',');
    }
}
