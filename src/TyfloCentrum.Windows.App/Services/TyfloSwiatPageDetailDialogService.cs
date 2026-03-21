using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TyfloCentrum.Windows.App.Views;

namespace TyfloCentrum.Windows.App.Services;

public sealed class TyfloSwiatPageDetailDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public TyfloSwiatPageDetailDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ShowAsync(
        int pageId,
        string fallbackTitle,
        string fallbackDate,
        string fallbackLink,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        if (xamlRoot is null)
        {
            return false;
        }

        var view = _serviceProvider.GetRequiredService<TyfloSwiatPageDetailView>();
        view.ViewModel.Initialize(pageId, fallbackTitle, fallbackDate, fallbackLink);
        ContentDialog? dialog = null;
        var requestedShare = false;
        view.ShareRequestHandler = () =>
        {
            requestedShare = true;
            dialog?.Hide();
            return Task.CompletedTask;
        };
        await view.ViewModel.LoadIfNeededAsync(cancellationToken);

        dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "TyfloŚwiat",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.Close,
            FullSizeDesired = true,
            Content = view,
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await dialog.ShowAsync();

            if (requestedShare)
            {
                await view.ViewModel.ShareAsync(cancellationToken);
                if (view.ViewModel.HasError)
                {
                    await ShowErrorDialogAsync(
                        xamlRoot,
                        view.ViewModel.ErrorMessage ?? "Nie udało się udostępnić strony TyfloŚwiata."
                    );
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Task ShowErrorDialogAsync(XamlRoot xamlRoot, string message)
    {
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
