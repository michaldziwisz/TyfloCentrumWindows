using System.Security;
using System.Text;
using TyfloCentrum.PushService.Models;

namespace TyfloCentrum.PushService.Services;

public static class WnsToastXmlBuilder
{
    public static string Build(PushDispatchPayload payload)
    {
        var builder = new StringBuilder();
        builder.Append("<toast launch=\"");
        builder.Append(BuildLaunchArguments(payload));
        builder.Append("\"><visual><binding template=\"ToastGeneric\">");
        builder.Append("<text>");
        builder.Append(Escape(payload.Title));
        builder.Append("</text><text>");
        builder.Append(Escape(payload.Body));
        builder.Append("</text></binding></visual></toast>");
        return builder.ToString();
    }

    private static string BuildLaunchArguments(PushDispatchPayload payload)
    {
        var pairs = new List<string> { $"kind={Uri.EscapeDataString(payload.Kind)}" };

        if (payload.Id is int id)
        {
            pairs.Add($"id={id}");
        }

        if (!string.IsNullOrWhiteSpace(payload.Title))
        {
            pairs.Add($"title={Uri.EscapeDataString(payload.Title)}");
        }

        if (!string.IsNullOrWhiteSpace(payload.Date))
        {
            pairs.Add($"date={Uri.EscapeDataString(payload.Date)}");
        }

        if (!string.IsNullOrWhiteSpace(payload.Link))
        {
            pairs.Add($"link={Uri.EscapeDataString(payload.Link)}");
        }

        return string.Join('&', pairs);
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }
}
