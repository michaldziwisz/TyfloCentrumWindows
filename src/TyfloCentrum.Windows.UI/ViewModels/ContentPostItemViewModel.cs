using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class ContentPostItemViewModel
{
    public ContentPostItemViewModel(ContentSource source, WpPostSummary item)
    {
        Source = source;
        PostId = item.Id;
        Title = WordPressTextFormatter.NormalizeHtml(item.Title.Rendered);
        Excerpt = WordPressTextFormatter.NormalizeHtml(item.Excerpt?.Rendered ?? string.Empty);
        Link = item.Link;
        PublishedDate = WordPressTextFormatter.FormatDate(item.Date);
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
            var parts = new List<string>
            {
                ItemTypeLabel,
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
