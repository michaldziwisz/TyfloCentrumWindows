using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class ContentPostItemViewModel : ObservableObject
{
    private ContentTypeAnnouncementPlacement _contentTypeAnnouncementPlacement;

    public ContentPostItemViewModel(
        ContentSource source,
        WpPostSummary item,
        ContentTypeAnnouncementPlacement contentTypeAnnouncementPlacement =
            ContentTypeAnnouncementPlacement.None
    )
    {
        Source = source;
        PostId = item.Id;
        Title = WordPressTextFormatter.NormalizeHtml(item.Title.Rendered);
        Excerpt = WordPressTextFormatter.NormalizeHtml(item.Excerpt?.Rendered ?? string.Empty);
        Link = item.Link;
        PublishedDate = WordPressTextFormatter.FormatDate(item.Date);
        _contentTypeAnnouncementPlacement = contentTypeAnnouncementPlacement;
    }

    public ContentSource Source { get; }

    public int PostId { get; }

    public string ItemTypeLabel => Source == ContentSource.Podcast ? "Podcast" : "Artykuł";

    public string Title { get; }

    public string Excerpt { get; }

    public string Link { get; }

    public string PublishedDate { get; }

    public bool SupportsPlayback => Source == ContentSource.Podcast;

    public string DefaultActionLabel =>
        SupportsPlayback
            ? $"Odtwórz podcast: {Title}"
            : $"Otwórz artykuł w aplikacji: {Title}";

    public string OpenLinkLabel =>
        $"Otwórz {ItemTypeLabel.ToLowerInvariant()} w zewnętrznej przeglądarce: {Title}";

    public string AccessibleLabel
    {
        get
        {
            var parts = new List<string>();

            switch (_contentTypeAnnouncementPlacement)
            {
                case ContentTypeAnnouncementPlacement.BeforeTitle:
                    parts.Add(ItemTypeLabel);
                    parts.Add(Title);
                    break;
                case ContentTypeAnnouncementPlacement.AfterTitle:
                    parts.Add(Title);
                    parts.Add(ItemTypeLabel);
                    break;
                default:
                    parts.Add(Title);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(PublishedDate))
            {
                parts.Add(PublishedDate);
            }

            return string.Join(". ", parts);
        }
    }

    public void SetContentTypeAnnouncementPlacement(ContentTypeAnnouncementPlacement placement)
    {
        if (_contentTypeAnnouncementPlacement == placement)
        {
            return;
        }

        _contentTypeAnnouncementPlacement = placement;
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    public override string ToString() => AccessibleLabel;
}
