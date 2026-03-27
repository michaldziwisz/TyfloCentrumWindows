using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class TyfloSwiatMagazineTocItemViewModel : ObservableObject
{
    private ContentTypeAnnouncementPlacement _contentTypeAnnouncementPlacement;

    public TyfloSwiatMagazineTocItemViewModel(
        WpPostSummary item,
        ContentTypeAnnouncementPlacement contentTypeAnnouncementPlacement =
            ContentTypeAnnouncementPlacement.None
    )
    {
        PageId = item.Id;
        Title = WordPressTextFormatter.NormalizeHtml(item.Title.Rendered);
        Link = item.Link;
        PublishedDate = WordPressTextFormatter.FormatDate(item.Date);
        _contentTypeAnnouncementPlacement = contentTypeAnnouncementPlacement;
    }

    public int PageId { get; }

    public string Title { get; }

    public string Link { get; }

    public string PublishedDate { get; }

    public string OpenDetailsLabel => $"Pokaż artykuł ze spisu treści: {Title}";

    public string OpenLinkLabel => $"Otwórz artykuł w przeglądarce: {Title}";

    [ObservableProperty]
    private bool isFavorite;

    public string FavoriteButtonText => IsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych";

    public string FavoriteButtonLabel => $"{FavoriteButtonText}: {BuildAccessibleTitle()}";

    public string AccessibleLabel
    {
        get
        {
            var parts = new List<string> { BuildAccessibleTitle() };

            if (!string.IsNullOrWhiteSpace(PublishedDate))
            {
                parts.Add(PublishedDate);
            }

            parts.Add(IsFavorite ? "W ulubionych" : "Poza ulubionymi");
            return string.Join(". ", parts);
        }
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteButtonText));
        OnPropertyChanged(nameof(FavoriteButtonLabel));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    public void SetContentTypeAnnouncementPlacement(ContentTypeAnnouncementPlacement placement)
    {
        if (_contentTypeAnnouncementPlacement == placement)
        {
            return;
        }

        _contentTypeAnnouncementPlacement = placement;
        OnPropertyChanged(nameof(FavoriteButtonLabel));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    private string BuildAccessibleTitle()
    {
        return _contentTypeAnnouncementPlacement switch
        {
            ContentTypeAnnouncementPlacement.BeforeTitle => $"Artykuł. {Title}",
            ContentTypeAnnouncementPlacement.AfterTitle => $"{Title}. Artykuł",
            _ => Title,
        };
    }

    public override string ToString() => AccessibleLabel;
}
