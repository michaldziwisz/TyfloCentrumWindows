using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.Domain.Catalog;

public static class AppSections
{
    public static readonly AppSection News = new(
        "news",
        "Nowości",
        "Wspólny feed nowych podcastów i artykułów.",
        1,
        "Alt+1"
    );

    public static readonly AppSection Podcasts = new(
        "podcasts",
        "Podcasty",
        "Kategorie, listy odcinków i szczegóły Tyflopodcast.",
        2,
        "Alt+2"
    );

    public static readonly AppSection Articles = new(
        "articles",
        "Artykuły",
        "Kategorie, wpisy i strony TyfloŚwiata.",
        3,
        "Alt+3"
    );

    public static readonly AppSection Search = new(
        "search",
        "Szukaj",
        "Wyszukiwanie treści z zakresem: podcasty, artykuły lub wszystko.",
        4,
        "Alt+4"
    );

    public static readonly AppSection Favorites = new(
        "favorites",
        "Ulubione",
        "Lokalna lista zapisanych podcastów i artykułów.",
        5,
        "Alt+5"
    );

    public static readonly AppSection Radio = new(
        "radio",
        "Tyfloradio",
        "Live stream, ramówka i kontakt z radiem.",
        6,
        "Alt+6"
    );

    public static readonly AppSection Settings = new(
        "settings",
        "Ustawienia",
        "Preferencje audio, urządzeń i odtwarzania dla tej aplikacji.",
        7,
        "Alt+7"
    );

    public static IReadOnlyList<AppSection> All { get; } = new[]
    {
        News,
        Podcasts,
        Articles,
        Search,
        Favorites,
        Radio,
        Settings,
    };
}
