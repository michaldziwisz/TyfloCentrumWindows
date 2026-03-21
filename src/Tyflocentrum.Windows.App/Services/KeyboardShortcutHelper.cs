using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace Tyflocentrum.Windows.App.Services;

internal static class KeyboardShortcutHelper
{
    public static bool IsControlPressed()
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(
            CoreVirtualKeyStates.Down
        );
    }
}
