using System.Text.RegularExpressions;

namespace TyfloCentrum.Windows.Domain.Text;

public static partial class DownloadFileNameSanitizer
{
    private const int MaxBaseNameLength = 120;

    private static readonly HashSet<string> ReservedFileNames =
    [
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    ];

    public static string CreateFileName(string? title, string fallbackBaseName, string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var safeBaseName = SanitizeBaseName(title);

        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = SanitizeBaseName(fallbackBaseName);
        }

        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = "plik";
        }

        if (ReservedFileNames.Contains(safeBaseName.ToUpperInvariant()))
        {
            safeBaseName = $"{safeBaseName}_plik";
        }

        if (safeBaseName.Length > MaxBaseNameLength)
        {
            safeBaseName = safeBaseName[..MaxBaseNameLength].TrimEnd(' ', '.');
        }

        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = "plik";
        }

        return $"{safeBaseName}{normalizedExtension}";
    }

    private static string SanitizeBaseName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new char[value.Length];
        var index = 0;
        var invalidCharacters = Path.GetInvalidFileNameChars();

        foreach (var character in value.Trim())
        {
            builder[index++] = invalidCharacters.Contains(character) || char.IsControl(character)
                ? ' '
                : character;
        }

        var sanitized = new string(builder, 0, index);
        sanitized = MultiWhitespaceRegex().Replace(sanitized, " ");
        sanitized = sanitized.Trim(' ', '.');
        return sanitized;
    }

    private static string NormalizeExtension(string extension)
    {
        var normalized = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.Trim();
        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiWhitespaceRegex();
}
