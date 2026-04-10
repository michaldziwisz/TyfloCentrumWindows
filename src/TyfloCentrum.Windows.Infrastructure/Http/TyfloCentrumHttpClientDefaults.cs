using System.Reflection;
using System.Net.Http.Headers;

namespace TyfloCentrum.Windows.Infrastructure.Http;

internal static class TyfloCentrumHttpClientDefaults
{
    private static readonly string UserAgent = BuildUserAgent();

    public static void ConfigureJsonClient(HttpClient client, TimeSpan timeout)
    {
        client.Timeout = timeout;
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        EnsureUserAgent(client);
    }

    public static void EnsureUserAgent(HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.Count > 0)
        {
            return;
        }

        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public static string GetUserAgent(HttpClient client)
    {
        EnsureUserAgent(client);
        return string.Join(" ", client.DefaultRequestHeaders.UserAgent.Select(static value => value.ToString()));
    }

    private static string BuildUserAgent()
    {
        var version =
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0.0";

        return $"TyfloCentrum.Windows.App/{version} (+https://github.com/michaldziwisz/TyfloCentrumWindows)";
    }
}
