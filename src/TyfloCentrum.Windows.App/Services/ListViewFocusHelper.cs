using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TyfloCentrum.Windows.App.Services;

internal static class ListViewFocusHelper
{
    public static void RestoreFocusedSelectionIfNeeded(ListView listView)
    {
        if (!FocusNavigationHelper.IsFocusWithin(listView) || listView.SelectedItem is null)
        {
            return;
        }

        var selectedItem = listView.SelectedItem;
        listView.DispatcherQueue.TryEnqueue(() => RestoreFocus(listView, selectedItem));
    }

    public static void RestoreFocus(ListView listView, object? item)
    {
        RestoreFocus(listView, item, remainingDeferredAttempts: 2);
    }

    private static void RestoreFocus(ListView listView, object? item, int remainingDeferredAttempts)
    {
        if (item is not null)
        {
            listView.SelectedItem = item;
            listView.ScrollIntoView(item);
            listView.UpdateLayout();

            if (listView.ContainerFromItem(item) is ListViewItem container)
            {
                container.Focus(FocusState.Programmatic);
                return;
            }

            if (remainingDeferredAttempts > 0)
            {
                listView.DispatcherQueue.TryEnqueue(() =>
                    RestoreFocus(listView, item, remainingDeferredAttempts - 1)
                );
                return;
            }
        }

        listView.Focus(FocusState.Programmatic);
    }
}
