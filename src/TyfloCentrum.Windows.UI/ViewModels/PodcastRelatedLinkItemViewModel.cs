namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed record PodcastRelatedLinkItemViewModel(
    string Title,
    Uri Url,
    string HostLabel,
    bool IsFavorite = false
)
{
    public string AccessibleLabel =>
        string.IsNullOrWhiteSpace(HostLabel) ? Title : $"{Title}. {HostLabel}.";

    public string OpenMenuLabel => $"Otwórz odnośnik: {Title}";

    public string CopyMenuLabel => $"Kopiuj odnośnik: {Title}";

    public string ShareMenuLabel => $"Udostępnij odnośnik (Ctrl+U): {Title}";

    public string FavoriteMenuLabel =>
        $"{(IsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych")} (Ctrl+D): odnośnik {Title}";

    public override string ToString() => AccessibleLabel;
}
