using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TyfloCentrum.Windows.App.Converters;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is true;
        if (parameter is string parameterText && string.Equals(parameterText, "Invert", StringComparison.Ordinal))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is Visibility visibility && visibility == Visibility.Visible;
        if (parameter is string parameterText && string.Equals(parameterText, "Invert", StringComparison.Ordinal))
        {
            isVisible = !isVisible;
        }

        return isVisible;
    }
}
