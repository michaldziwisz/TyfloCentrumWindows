using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.Infrastructure.Playback;

/// <summary>
/// Fabryka progresywnych buforów. Ma własny <see cref="HttpClient"/> (przez
/// IHttpClientFactory / typed client) z długim timeoutem, bo strumień audio bywa
/// otwarty przez cały czas odtwarzania odcinka.
/// </summary>
public sealed class ProgressiveMediaCacheFactory : IProgressiveMediaCacheFactory
{
    private readonly HttpClient _httpClient;

    public ProgressiveMediaCacheFactory(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public IProgressiveMediaCache Create(Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        return new ProgressiveMediaCache(_httpClient, sourceUri);
    }
}
