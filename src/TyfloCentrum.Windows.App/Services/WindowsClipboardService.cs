using TyfloCentrum.Windows.Domain.Services;
using Windows.ApplicationModel.DataTransfer;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsClipboardService : IClipboardService
{
    public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(false);
        }

        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
