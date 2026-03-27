using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class ContentTypeAnnouncementPlacementOptionViewModel
{
    public ContentTypeAnnouncementPlacementOptionViewModel(
        ContentTypeAnnouncementPlacement value,
        string label
    )
    {
        Value = value;
        Label = label;
    }

    public ContentTypeAnnouncementPlacement Value { get; }

    public string Label { get; }
}
