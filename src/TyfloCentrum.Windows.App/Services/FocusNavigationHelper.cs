using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TyfloCentrum.Windows.App.Services;

internal static class FocusNavigationHelper
{
    public static bool IsFocusWithin(FrameworkElement element)
    {
        if (element.XamlRoot is null)
        {
            return false;
        }

        var current = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(element.XamlRoot) as DependencyObject;
        while (current is not null)
        {
            if (ReferenceEquals(current, element))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
