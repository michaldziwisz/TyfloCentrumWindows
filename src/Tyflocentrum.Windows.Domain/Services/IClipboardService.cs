namespace Tyflocentrum.Windows.Domain.Services;

public interface IClipboardService
{
    Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default);
}
