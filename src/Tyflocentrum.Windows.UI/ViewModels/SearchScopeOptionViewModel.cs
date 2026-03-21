using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.UI.ViewModels;

public sealed class SearchScopeOptionViewModel
{
    private SearchScopeOptionViewModel(SearchScope value, string title)
    {
        Value = value;
        Title = title;
    }

    public SearchScope Value { get; }

    public string Title { get; }

    public static IReadOnlyList<SearchScopeOptionViewModel> All { get; } =
    [
        new(SearchScope.All, "Wszystko"),
        new(SearchScope.Podcasts, "Podcasty"),
        new(SearchScope.Articles, "Artykuły"),
    ];

    public override string ToString() => Title;
}
