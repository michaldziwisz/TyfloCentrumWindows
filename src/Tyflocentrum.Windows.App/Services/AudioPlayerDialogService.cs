using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tyflocentrum.Windows.App.Views;
using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.App.Services;

public sealed class AudioPlayerDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public AudioPlayerDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ShowAsync(
        AudioPlaybackRequest request,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        if (xamlRoot is null)
        {
            return false;
        }

        var view = _serviceProvider.GetRequiredService<AudioPlayerView>();

        try
        {
            await view.InitializeAsync(request, cancellationToken);
        }
        catch
        {
            await view.StopAndDisposePlayerAsync();
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Odtwarzacz",
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            await view.StopAndDisposePlayerAsync();
        }
    }
}
