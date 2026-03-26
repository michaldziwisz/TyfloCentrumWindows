using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace TyfloCentrum.Windows.App.Services;

internal static class KeyboardShortcutHelper
{
    public static bool IsControlPressed()
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(
            CoreVirtualKeyStates.Down
        );
    }

    public static bool IsAltPressed()
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(
            CoreVirtualKeyStates.Down
        );
    }
}
