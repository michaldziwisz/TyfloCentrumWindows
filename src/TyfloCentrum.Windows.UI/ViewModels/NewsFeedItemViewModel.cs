using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class NewsFeedItemViewModel : ObservableObject
{
    private ContentTypeAnnouncementPlacement _contentTypeAnnouncementPlacement;

    public NewsFeedItemViewModel(
        NewsFeedItem item,
        ContentTypeAnnouncementPlacement contentTypeAnnouncementPlacement =
            ContentTypeAnnouncementPlacement.None
    )
    {
        Kind = item.Kind;
        PostId = item.Post.Id;
        Title = WordPressTextFormatter.NormalizeHtml(item.Post.Title.Rendered);
        Excerpt = WordPressTextFormatter.NormalizeHtml(item.Post.Excerpt?.Rendered ?? string.Empty);
        Link = item.Post.Link;
        PublishedDate = WordPressTextFormatter.FormatDate(item.Post.Date);
        _contentTypeAnnouncementPlacement = contentTypeAnnouncementPlacement;
    }

    public NewsItemKind Kind { get; }

    public int PostId { get; }

    public ContentSource Source => Kind == NewsItemKind.Podcast ? ContentSource.Podcast : ContentSource.Article;

    public string KindLabel => Kind == NewsItemKind.Podcast ? "Podcast" : "Artykuł";

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
        $"Otwórz {KindLabel.ToLowerInvariant()} w zewnętrznej przeglądarce: {Title}";

    public string AccessibleLabel
    {
        get
        {
            var parts = new List<string>();

            switch (_contentTypeAnnouncementPlacement)
            {
                case ContentTypeAnnouncementPlacement.BeforeTitle:
                    parts.Add(KindLabel);
                    parts.Add(Title);
                    break;
                case ContentTypeAnnouncementPlacement.AfterTitle:
                    parts.Add(Title);
                    parts.Add(KindLabel);
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
