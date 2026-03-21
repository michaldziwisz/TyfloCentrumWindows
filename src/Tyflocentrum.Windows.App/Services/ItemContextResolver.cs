using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Tyflocentrum.Windows.App.Services;

internal static class ItemContextResolver
{
    public static T? Resolve<T>(object? originalSource)
        where T : class
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: T item })
            {
                return item;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
