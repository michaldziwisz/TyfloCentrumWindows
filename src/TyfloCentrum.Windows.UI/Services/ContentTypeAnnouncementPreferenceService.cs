using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.Services;

public sealed class ContentTypeAnnouncementPreferenceService
{
    private ContentTypeAnnouncementPlacement _placement = ContentTypeAnnouncementPlacement.None;

    public event EventHandler? Changed;

    public ContentTypeAnnouncementPlacement Placement => _placement;

    public async Task InitializeAsync(
        IAppSettingsService appSettingsService,
        CancellationToken cancellationToken = default
    )
    {
        var settings = await appSettingsService.GetAsync(cancellationToken);
        SetPlacement(settings.ContentTypeAnnouncementPlacement);
    }

    public void SetPlacement(ContentTypeAnnouncementPlacement placement)
    {
        if (_placement == placement)
        {
            return;
        }

        _placement = placement;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
