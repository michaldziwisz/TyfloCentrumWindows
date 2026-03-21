using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TyfloCentrum.Windows.App.Services;

internal static class DialogHelpers
{
    public static Task ShowErrorAsync(XamlRoot? xamlRoot, string message)
    {
        if (xamlRoot is null)
        {
            return Task.CompletedTask;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Błąd",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.Close,
            Content = message,
        };

        return dialog.ShowAsync().AsTask();
    }
}
