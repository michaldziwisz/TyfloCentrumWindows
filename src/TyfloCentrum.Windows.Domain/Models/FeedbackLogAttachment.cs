namespace TyfloCentrum.Windows.Domain.Models;

public sealed record FeedbackLogAttachment(
    string FileName,
    string ContentType,
    string Encoding,
    string DataBase64,
    int OriginalBytes,
    bool Truncated
);
