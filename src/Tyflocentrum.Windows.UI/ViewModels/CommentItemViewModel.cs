using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Text;

namespace Tyflocentrum.Windows.UI.ViewModels;

public sealed class CommentItemViewModel
{
    private const int PreviewLength = 240;

    public CommentItemViewModel(WordPressComment item)
    {
        Id = item.Id;
        AuthorName = item.AuthorName;
        BodyText = WordPressContentText.ToReadablePlainText(item.Content.Rendered);
    }

    public int Id { get; }

    public string AuthorName { get; }

    public string BodyText { get; }

    public string BodyPreviewText => BuildPreview(BodyText);

    public bool HasTruncatedPreview => !string.Equals(BodyPreviewText, BodyText, StringComparison.Ordinal);

    public string DetailsButtonLabel => $"Pokaż szczegóły komentarza: {AuthorName}";

    public string AccessibleLabel =>
        string.IsNullOrWhiteSpace(BodyPreviewText) ? AuthorName : $"{AuthorName}. {BodyPreviewText}";

    public override string ToString() => AccessibleLabel;

    private static string BuildPreview(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= PreviewLength)
        {
            return value;
        }

        return $"{value[..PreviewLength].TrimEnd()}…";
    }
}
