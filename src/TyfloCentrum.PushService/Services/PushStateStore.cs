using System.Text.Json;
using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Options;
using Microsoft.Extensions.Options;

namespace TyfloCentrum.PushService.Services;

public sealed class PushStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly PushServiceOptions _options;
    private readonly ILogger<PushStateStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _statePath;

    public PushStateStore(
        IHostEnvironment hostEnvironment,
        IOptions<PushServiceOptions> options,
        ILogger<PushStateStore> logger
    )
    {
        _options = options.Value;
        _logger = logger;

        var dataDirectory = Path.IsPathRooted(_options.DataDirectory)
            ? _options.DataDirectory
            : Path.Combine(hostEnvironment.ContentRootPath, _options.DataDirectory);

        Directory.CreateDirectory(dataDirectory);
        _statePath = Path.Combine(dataDirectory, _options.StateFileName);
    }

    public async Task<PushServiceState> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadCoreAsync(cancellationToken)).Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(
        Action<PushServiceState> update,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(update);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadCoreAsync(cancellationToken);
            update(state);
            Prune(state);
            await SaveCoreAsync(state, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PushServiceState> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            return new PushServiceState();
        }

        await using var stream = File.OpenRead(_statePath);
        var state = await JsonSerializer.DeserializeAsync<PushServiceState>(
            stream,
            SerializerOptions,
            cancellationToken
        );

        return state ?? new PushServiceState();
    }

    private async Task SaveCoreAsync(PushServiceState state, CancellationToken cancellationToken)
    {
        var tempPath = $"{_statePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, _statePath, overwrite: true);
    }

    private void Prune(PushServiceState state)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromDays(Math.Max(1, _options.TokenTtlDays));

        foreach (var token in state.Tokens
                     .Where(pair => IsExpired(pair.Value, nowUtc, ttl))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            state.Tokens.Remove(token);
        }

        if (state.Tokens.Count <= _options.MaxTokens)
        {
            TrimSentIds(state);
            return;
        }

        foreach (var token in state.Tokens
                     .OrderBy(pair => pair.Value.LastSeenAt, StringComparer.Ordinal)
                     .Take(state.Tokens.Count - _options.MaxTokens)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            state.Tokens.Remove(token);
        }

        TrimSentIds(state);
        _logger.LogInformation("Pruned push-service state to {TokenCount} tokens.", state.Tokens.Count);
    }

    private static bool IsExpired(
        PushSubscriberRecord record,
        DateTimeOffset nowUtc,
        TimeSpan ttl
    )
    {
        if (!DateTimeOffset.TryParse(record.LastSeenAt, out var lastSeenAt))
        {
            return false;
        }

        return nowUtc - lastSeenAt > ttl;
    }

    private static void TrimSentIds(PushServiceState state)
    {
        TrimList(state.SentPodcastIds);
        TrimList(state.SentArticleIds);
    }

    private static void TrimList(List<int> values)
    {
        const int maxItems = 500;
        if (values.Count <= maxItems)
        {
            return;
        }

        values.RemoveRange(maxItems, values.Count - maxItems);
    }
}
