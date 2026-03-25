using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class NewsFeedItemViewModel
{
    public NewsFeedItemViewModel(NewsFeedItem item)
    {
        Kind = item.Kind;
        PostId = item.Post.Id;
        Title = WordPressTextFormatter.NormalizeHtml(item.Post.Title.Rendered);
        Excerpt = WordPressTextFormatter.NormalizeHtml(item.Post.Excerpt?.Rendered ?? string.Empty);
        Link = item.Post.Link;
        PublishedDate = WordPressTextFormatter.FormatDate(item.Post.Date);
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
            var parts = new List<string>
            {
                KindLabel,
                Title,
            };

            if (!string.IsNullOrWhiteSpace(PublishedDate))
            {
                parts.Add(PublishedDate);
            }

            if (!string.IsNullOrWhiteSpace(Excerpt))
            {
                parts.Add(Excerpt);
            }

            return string.Join(". ", parts);
        }
    }

    public override string ToString() => AccessibleLabel;
}
