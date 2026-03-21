using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tyflocentrum.Windows.App.Views;

namespace Tyflocentrum.Windows.App.Services;

public sealed class ContactTextMessageDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public ContactTextMessageDialogService(IServiceProvider serviceProvider)
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

        var view = _serviceProvider.GetRequiredService<ContactTextMessageView>();
        await view.ViewModel.LoadIfNeededAsync(cancellationToken);

        var didSend = false;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Kontakt z Tyfloradiem",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.Close,
            FullSizeDesired = false,
            Content = view,
        };

        void OnMessageSent(object? sender, EventArgs e)
        {
            didSend = true;
            dialog.Hide();
        }

        view.ViewModel.MessageSent += OnMessageSent;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await dialog.ShowAsync();
            return didSend;
        }
        finally
        {
            view.ViewModel.MessageSent -= OnMessageSent;
        }
    }
}
