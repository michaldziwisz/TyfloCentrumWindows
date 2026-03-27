namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed record PodcastChapterMarkerItemViewModel(
    string Title,
    double Seconds,
    string TimeLabel,
    bool IsFavorite = false
)
{
    public string AccessibleLabel => $"{Title}. {TimeLabel}.";

    public string FavoriteMenuLabel =>
        $"{(IsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych")} (Ctrl+D): temat {Title}";

    public override string ToString() => AccessibleLabel;
}
