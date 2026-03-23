using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.Domain.Text;

public static class NotificationActivationRequestParser
{
    public static NotificationActivationRequest? Parse(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        var values = ParseQueryString(arguments);

        if (!values.TryGetValue("kind", out var kindValue) || !values.TryGetValue("id", out var idValue))
        {
            return null;
        }

        if (!int.TryParse(idValue, out var postId) || postId <= 0)
        {
            return null;
        }

        var source = kindValue.ToLowerInvariant() switch
        {
            "podcast" => ContentSource.Podcast,
            "article" => ContentSource.Article,
            _ => (ContentSource?)null,
        };

        if (source is null)
        {
            return null;
        }

        values.TryGetValue("title", out var title);
        values.TryGetValue("date", out var date);
        values.TryGetValue("link", out var link);

        return new NotificationActivationRequest(
            source.Value,
            postId,
            string.IsNullOrWhiteSpace(title) ? "TyfloCentrum" : title,
            string.IsNullOrWhiteSpace(date) ? null : date,
            string.IsNullOrWhiteSpace(link) ? null : link
        );
    }

    private static Dictionary<string, string> ParseQueryString(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in value.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(segment[..separatorIndex]);
            var segmentValue = Uri.UnescapeDataString(segment[(separatorIndex + 1)..]);
            result[key] = segmentValue;
        }

        return result;
    }
}
