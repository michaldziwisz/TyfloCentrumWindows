using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.App.Services;

public sealed class NotificationActivationService
{
    private NotificationActivationRequest? _pendingRequest;

    public event EventHandler? PendingRequestChanged;

    public void HandleArguments(string? arguments)
    {
        var request = NotificationActivationRequestParser.Parse(arguments);
        if (request is null)
        {
            return;
        }

        _pendingRequest = request;
        PendingRequestChanged?.Invoke(this, EventArgs.Empty);
    }

    public NotificationActivationRequest? TakePendingRequest()
    {
        var request = _pendingRequest;
        _pendingRequest = null;
        return request;
    }
}
