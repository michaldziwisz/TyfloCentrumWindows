using CommunityToolkit.Mvvm.ComponentModel;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class CommentDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private string authorName = string.Empty;

    [ObservableProperty]
    private string bodyText = string.Empty;

    public string DialogTitle => string.IsNullOrWhiteSpace(AuthorName) ? "Komentarz" : $"Komentarz: {AuthorName}";

    public string BodyDisplayText =>
        string.IsNullOrWhiteSpace(BodyText) ? "Brak treści komentarza." : BodyText;

    public string AccessibleLabel => $"{DialogTitle}. {BodyDisplayText}";

    public void Initialize(CommentItemViewModel item)
    {
        AuthorName = item.AuthorName;
        BodyText = item.BodyText;
        OnPropertyChanged(nameof(DialogTitle));
        OnPropertyChanged(nameof(BodyDisplayText));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    partial void OnAuthorNameChanged(string value)
    {
        OnPropertyChanged(nameof(DialogTitle));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    partial void OnBodyTextChanged(string value)
    {
        OnPropertyChanged(nameof(BodyDisplayText));
        OnPropertyChanged(nameof(AccessibleLabel));
    }
}
