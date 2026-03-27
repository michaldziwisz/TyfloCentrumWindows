using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class CommentItemViewModel : ObservableObject
{
    private const int PreviewLength = 240;

    public CommentItemViewModel(WordPressComment item)
    {
        Id = item.Id;
        ParentId = item.ParentId;
        AuthorName = item.AuthorName;
        PublishedAtUtc = item.PublishedAtUtc;
        BodyText = WordPressContentText.ToReadablePlainText(item.Content.Rendered);
    }

    public int Id { get; }

    public int ParentId { get; }

    public string AuthorName { get; }

    public DateTimeOffset? PublishedAtUtc { get; }

    public string BodyText { get; }

    public string BodyPreviewText => BuildPreview(BodyText);

    public string BodyPreviewDisplayText =>
        string.IsNullOrWhiteSpace(BodyPreviewText) ? "Brak treści komentarza." : BodyPreviewText;

    public string BodyDisplayText => IsExpanded ? FullBodyDisplayText : BodyPreviewDisplayText;

    public string FullBodyDisplayText =>
        string.IsNullOrWhiteSpace(BodyText) ? "Brak treści komentarza." : BodyText;

    public bool HasTruncatedPreview => !string.Equals(BodyPreviewText, BodyText, StringComparison.Ordinal);

    public bool IsReply => !string.IsNullOrWhiteSpace(ReplyToAuthorName);

    public string ReplyContextText =>
        IsReply ? $"Odpowiedź do: {ReplyToAuthorName}" : string.Empty;

    public string ReplyContextVisibilityValue => IsReply ? "Visible" : "Collapsed";

    public string ContainerMarginValue => $"{Math.Min(ThreadDepth, 4) * 24},0,0,8";

    public string ReplyAccentBorderThicknessValue => IsReply ? "3,0,0,0" : "0";

    public string ReplyAccentPaddingValue => IsReply ? "12,0,0,0" : "0";

    public string DetailsButtonText =>
        IsExpanded ? "Ukryj szczegóły komentarza" : "Szczegóły komentarza";

    public string DetailsButtonLabel => $"{DetailsButtonText}: {AuthorName}";

    public string AccessibleLabel =>
        IsReply
            ? $"{AuthorName}, odpowiedź do {ReplyToAuthorName}. {BodyDisplayText}"
            : $"{AuthorName}. {BodyDisplayText}";

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private int threadDepth;

    [ObservableProperty]
    private string? replyToAuthorName;

    public void ApplyThreadContext(int depth, string? parentAuthorName)
    {
        ThreadDepth = Math.Max(depth, 0);
        ReplyToAuthorName = string.IsNullOrWhiteSpace(parentAuthorName) ? null : parentAuthorName.Trim();
    }

    public override string ToString() => AccessibleLabel;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(BodyDisplayText));
        OnPropertyChanged(nameof(DetailsButtonText));
        OnPropertyChanged(nameof(DetailsButtonLabel));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    partial void OnThreadDepthChanged(int value)
    {
        OnPropertyChanged(nameof(ContainerMarginValue));
        OnPropertyChanged(nameof(ReplyContextText));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    partial void OnReplyToAuthorNameChanged(string? value)
    {
        OnPropertyChanged(nameof(IsReply));
        OnPropertyChanged(nameof(ReplyContextVisibilityValue));
        OnPropertyChanged(nameof(ReplyAccentBorderThicknessValue));
        OnPropertyChanged(nameof(ReplyAccentPaddingValue));
        OnPropertyChanged(nameof(ReplyContextText));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    private static string BuildPreview(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= PreviewLength)
        {
            return value;
        }

        return $"{value[..PreviewLength].TrimEnd()}…";
    }
}
