using CommunityToolkit.Mvvm.ComponentModel;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.UI.Formatting;

namespace Tyflocentrum.Windows.UI.ViewModels;

public partial class TyfloSwiatMagazineTocItemViewModel : ObservableObject
{
    public TyfloSwiatMagazineTocItemViewModel(WpPostSummary item)
    {
        PageId = item.Id;
        Title = WordPressTextFormatter.NormalizeHtml(item.Title.Rendered);
        Link = item.Link;
        PublishedDate = WordPressTextFormatter.FormatDate(item.Date);
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

    public string FavoriteButtonLabel => $"{FavoriteButtonText}: artykuł {Title}";

    public string AccessibleLabel
    {
        get
        {
            var parts = new List<string> { "Artykuł", Title };

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

    public override string ToString() => AccessibleLabel;
}
