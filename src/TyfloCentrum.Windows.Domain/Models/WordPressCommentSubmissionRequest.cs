namespace TyfloCentrum.Windows.Domain.Models;

public sealed record WordPressCommentSubmissionRequest(
    int PostId,
    string AuthorName,
    string AuthorEmail,
    string Content,
    int ParentId = 0
);
