using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.UI.ViewModels;

public sealed class FavoriteItemViewModel
{
    public FavoriteItemViewModel(FavoriteItem item)
    {
        Id = item.Id;
        Kind = item.ResolvedKind;
        ArticleOrigin = item.ResolvedArticleOrigin;
        Source = item.Source;
        PostId = item.PostId;
        Title = item.Title;
        Subtitle = item.Subtitle;
        PublishedDate = item.PublishedDate;
        Link = item.Link;
        ContextTitle = item.ContextTitle;
        ContextSubtitle = item.ContextSubtitle;
        StartPositionSeconds = item.StartPositionSeconds;
        SavedAtUtc = item.SavedAtUtc;
    }

    public string Id { get; }

    public FavoriteKind Kind { get; }

    public FavoriteArticleOrigin ArticleOrigin { get; }

    public ContentSource Source { get; }

    public int PostId { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string PublishedDate { get; }

    public string Link { get; }

    public string ContextTitle { get; }

    public string ContextSubtitle { get; }

    public double? StartPositionSeconds { get; }

    public DateTimeOffset SavedAtUtc { get; }

    public string ItemTypeLabel =>
        Kind switch
        {
            FavoriteKind.Podcast => "Podcast",
            FavoriteKind.Article when ArticleOrigin == FavoriteArticleOrigin.Page => "Artykuł TyfloŚwiata",
            FavoriteKind.Article => "Artykuł",
            FavoriteKind.Topic => "Temat",
            FavoriteKind.Link => "Odnośnik",
            _ => "Pozycja",
        };

    public string SecondaryText => Subtitle;

    public bool HasSecondaryText => !string.IsNullOrWhiteSpace(SecondaryText);

    public bool HasPublishedDate => !string.IsNullOrWhiteSpace(PublishedDate);

    public string ContextLabel =>
        Kind switch
        {
            FavoriteKind.Link when !string.IsNullOrWhiteSpace(ContextTitle) =>
                $"W podcaście: {ContextTitle}",
            _ => string.Empty,
        };

    public bool HasContextLabel => !string.IsNullOrWhiteSpace(ContextLabel);

    public string SavedAtLabel => $"Dodano {SavedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}";

    public bool HasExternalOpenAction =>
        Kind == FavoriteKind.Podcast || Kind == FavoriteKind.Article;

    public string OpenLinkLabel => $"Otwórz {ItemTypeLabel.ToLowerInvariant()} w przeglądarce: {Title}";

    public bool HasCopyLinkAction => !string.IsNullOrWhiteSpace(Link);

    public string CopyLinkLabel => $"Kopiuj odnośnik: {Title}";

    public bool HasShareLinkAction => !string.IsNullOrWhiteSpace(Link);

    public string ShareLinkLabel => $"Udostępnij odnośnik: {Title}";

    public string RemoveLabel => $"Usuń z ulubionych: {Title}";

    public string AccessibleLabel
    {
        get
        {
            var parts = new List<string>
            {
                ItemTypeLabel,
                Title,
            };

            if (!string.IsNullOrWhiteSpace(SecondaryText))
            {
                parts.Add(SecondaryText);
            }

            if (!string.IsNullOrWhiteSpace(PublishedDate))
            {
                parts.Add(PublishedDate);
            }

            if (!string.IsNullOrWhiteSpace(ContextLabel))
            {
                parts.Add(ContextLabel);
            }

            parts.Add(SavedAtLabel);
            return string.Join(". ", parts);
        }
    }

    public override string ToString() => AccessibleLabel;
}
