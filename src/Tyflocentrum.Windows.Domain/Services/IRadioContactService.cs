using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Services;

public interface IRadioContactService
{
    Task<ContactSubmissionResult> SendMessageAsync(
        string author,
        string comment,
        CancellationToken cancellationToken = default
    );
}
