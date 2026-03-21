namespace Tyflocentrum.Windows.Domain.Models;

public sealed record AppSection(
    string Key,
    string Title,
    string Description,
    int ShortcutNumber,
    string ShortcutLabel
);
