using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.Infrastructure.Storage;

public sealed class LocalPlaybackResumeService : IPlaybackResumeService
{
    private const double MinimumResumePositionSeconds = 1d;
    private const string ResumePositionPrefix = "resume.playback.";

    private readonly ILocalSettingsStore _localSettingsStore;

    public LocalPlaybackResumeService(ILocalSettingsStore localSettingsStore)
    {
        _localSettingsStore = localSettingsStore;
    }

    public async Task<double?> GetResumePositionAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken = default
    )
    {
        var storedValue = await _localSettingsStore.GetStringAsync(
            CreateStorageKey(sourceUrl),
            cancellationToken
        );

        if (
            !double.TryParse(
                storedValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed
            )
            || double.IsNaN(parsed)
            || double.IsInfinity(parsed)
            || parsed <= MinimumResumePositionSeconds
        )
        {
            return null;
        }

        return parsed;
    }

    public Task SaveResumePositionAsync(
        Uri sourceUrl,
        double positionSeconds,
        CancellationToken cancellationToken = default
    )
    {
        if (
            double.IsNaN(positionSeconds)
            || double.IsInfinity(positionSeconds)
            || positionSeconds <= MinimumResumePositionSeconds
        )
        {
            return ClearResumePositionAsync(sourceUrl, cancellationToken);
        }

        return _localSettingsStore
            .SetStringAsync(
                CreateStorageKey(sourceUrl),
                positionSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                cancellationToken
            )
            .AsTask();
    }

    public Task ClearResumePositionAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken = default
    )
    {
        return _localSettingsStore
            .DeleteStringAsync(CreateStorageKey(sourceUrl), cancellationToken)
            .AsTask();
    }

    private static string CreateStorageKey(Uri sourceUrl)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);

        var normalized = sourceUrl.AbsoluteUri.Trim();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{ResumePositionPrefix}{Convert.ToHexString(hash)}";
    }
}
