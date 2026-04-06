using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TyfloCentrum.Windows.App.Views;

namespace TyfloCentrum.Windows.App.Services;

public sealed class ContactTextMessageDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private bool _isShowing;

    public ContactTextMessageDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<FormDialogResult> ShowAsync(
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        xamlRoot = ResolveDialogXamlRoot(xamlRoot);
        if (xamlRoot is null)
        {
            return FormDialogResult.FailedToOpen;
        }

        if (_isShowing)
        {
            return FormDialogResult.Closed;
        }

        var view = _serviceProvider.GetRequiredService<ContactTextMessageView>();
        await view.ViewModel.LoadIfNeededAsync(cancellationToken);

        var didSend = false;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Kontakt z Tyfloradiem",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.None,
            FullSizeDesired = false,
            Content = view,
        };

        void OnMessageSent(object? sender, EventArgs e)
        {
            didSend = true;
            dialog.Hide();
        }

        view.ViewModel.MessageSent += OnMessageSent;
        dialog.Opened += OnDialogOpened;

        try
        {
            _isShowing = true;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            await dialog.ShowAsync();
            return didSend ? FormDialogResult.Submitted : FormDialogResult.Closed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return FormDialogResult.FailedToOpen;
        }
        finally
        {
            _isShowing = false;
            view.ViewModel.MessageSent -= OnMessageSent;
            dialog.Opened -= OnDialogOpened;
        }

        void OnDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            view.DispatcherQueue.TryEnqueue(view.FocusPrimaryContent);
        }
    }

    private XamlRoot? ResolveDialogXamlRoot(XamlRoot? preferredXamlRoot)
    {
        if (preferredXamlRoot is not null)
        {
            return preferredXamlRoot;
        }

        return (_serviceProvider.GetService<MainWindow>()?.Content as FrameworkElement)?.XamlRoot;
    }
}
