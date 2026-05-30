namespace TyfloCentrum.Windows.Domain.Models;

public sealed record PodcastTextVersionDocument(
    string Title,
    string PublishedDate,
    string Link,
    string ReaderHtml
);
