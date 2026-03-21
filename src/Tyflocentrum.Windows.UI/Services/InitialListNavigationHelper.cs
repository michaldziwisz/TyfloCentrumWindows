using System.Globalization;
using System.Text;

namespace Tyflocentrum.Windows.UI.Services;

public static class InitialListNavigationHelper
{
    public static T? FindNextByInitial<T>(
        IReadOnlyList<T> items,
        int currentIndex,
        Func<T, string> textSelector,
        char input
    )
    {
        if (items.Count == 0)
        {
            return default;
        }

        var normalizedInput = Normalize(input.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return default;
        }

        var startIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % items.Count;
        for (var offset = 0; offset < items.Count; offset++)
        {
            var index = (startIndex + offset) % items.Count;
            var item = items[index];
            var text = Normalize(textSelector(item));
            if (text.StartsWith(normalizedInput, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return default;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
