namespace TyfloCentrum.PushService.Options;

public sealed class PushServiceOptions
{
    public string DataDirectory { get; set; } = "./.data";

    public string StateFileName { get; set; } = "state.json";

    public int TokenTtlDays { get; set; } = 60;

    public int MaxTokens { get; set; } = 50_000;

    public int PollIntervalSeconds { get; set; } = 900;

    public int PollPerPage { get; set; } = 20;

    public string WebhookSecret { get; set; } = string.Empty;

    public string TyflopodcastWordPressBaseUrl { get; set; } = "https://tyflopodcast.net/wp-json/";

    public string TyfloswiatWordPressBaseUrl { get; set; } = "https://tyfloswiat.pl/wp-json/";

    public string AzureTenantId { get; set; } = string.Empty;

    public string AzureClientId { get; set; } = string.Empty;

    public string AzureClientSecret { get; set; } = string.Empty;

    public string WnsScope { get; set; } = "https://wns.windows.com/.default";
}
