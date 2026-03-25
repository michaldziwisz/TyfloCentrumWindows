namespace TyfloCentrum.Windows.Infrastructure.Http;

public sealed class TyfloCentrumEndpointsOptions
{
    public Uri TyflopodcastApiBaseUrl { get; init; } = new("https://tyflopodcast.net/wp-json/");

    public Uri TyfloswiatApiBaseUrl { get; init; } = new("https://tyfloswiat.pl/wp-json/");

    public Uri TyflopodcastDownloadUrl { get; init; } = new("https://tyflopodcast.net/pobierz.php");

    public Uri ContactPanelBaseUrl { get; init; } = new("https://kontakt.tyflopodcast.net/json.php");

    public Uri PushServiceBaseUrl { get; init; } = new("https://tyflocentrum.tyflo.eu.org/");

    public Uri? SygnalistaBaseUrl { get; init; }

    public string SygnalistaAppId { get; init; } = "tyflocentrum";

    public string? SygnalistaAppToken { get; init; }

    public string PushAzureAppId { get; init; } = "6f96d75e-3f7c-46a4-bfe2-0bfdd8ddf2d1";

    public string? PushAzureObjectId { get; init; }

    public Uri TyfloradioStreamUrl { get; init; } = new("https://radio.tyflopodcast.net/hls/stream.m3u8");
}
