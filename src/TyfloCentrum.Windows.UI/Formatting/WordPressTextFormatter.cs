using System.Globalization;
using TyfloCentrum.Windows.Domain.Text;

namespace TyfloCentrum.Windows.UI.Formatting;

public static class WordPressTextFormatter
{
    public static string NormalizeHtml(string value) => WordPressContentText.NormalizeHtml(value);

    public static string FormatDate(string value)
    {
        if (
            DateTime.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed
            )
        )
        {
            return parsed.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        return value;
    }
}
