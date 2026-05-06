using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TyfloCentrum.Windows.App.Views;
using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.App.Services;

public sealed class AudioPlayerDialogService
{
    private const double DialogChromeReservedHeight = 220;
    private const double MinimumScrollableContentHeight = 320;

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

        var scrollHost = new ScrollViewer
        {
            Content = view,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            IsTabStop = false,
            MaxHeight = CalculateScrollableContentMaxHeight(xamlRoot),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Auto,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Odtwarzacz",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.Close,
            FullSizeDesired = true,
            Content = scrollHost,
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

    private static double CalculateScrollableContentMaxHeight(XamlRoot xamlRoot)
    {
        var availableHeight = xamlRoot.Size.Height - DialogChromeReservedHeight;
        return Math.Max(MinimumScrollableContentHeight, availableHeight);
    }
}
