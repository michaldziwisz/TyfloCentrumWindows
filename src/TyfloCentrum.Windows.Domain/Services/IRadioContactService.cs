using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Services;

public interface IRadioContactService
{
    Task<ContactSubmissionResult> SendMessageAsync(
        string author,
        string comment,
        CancellationToken cancellationToken = default
    );
}
