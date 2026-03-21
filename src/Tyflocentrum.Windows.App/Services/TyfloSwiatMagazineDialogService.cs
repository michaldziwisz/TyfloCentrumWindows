using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tyflocentrum.Windows.App.Views;

namespace Tyflocentrum.Windows.App.Services;

public sealed class TyfloSwiatMagazineDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public TyfloSwiatMagazineDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ShowAsync(
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        if (xamlRoot is null)
        {
            return false;
        }

        var view = _serviceProvider.GetRequiredService<TyfloSwiatMagazineView>();
        await view.ViewModel.LoadIfNeededAsync(cancellationToken);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Czasopismo TyfloŚwiat",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.Close,
            FullSizeDesired = true,
            Content = view,
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await dialog.ShowAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
