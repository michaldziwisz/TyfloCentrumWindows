namespace TyfloCentrum.Windows.Domain.Models;

public sealed record AppSection(
    string Key,
    string Title,
    string Description,
    int ShortcutNumber,
    string ShortcutLabel
)
{
    public string DisplayTitle => $"{Title} ({ShortcutLabel})";
}
