namespace TyfloCentrum.PushService.Models;

public sealed record WordPressPostEnvelope(
    int Id,
    string Date,
    string Link,
    WordPressRenderedText Title
);

public sealed record WordPressRenderedText(string Rendered);
