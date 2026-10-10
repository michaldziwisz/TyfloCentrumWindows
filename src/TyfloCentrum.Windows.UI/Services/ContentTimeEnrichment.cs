using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.UI.Services;

/// <summary>Uzupełnia istniejące wiersze, nigdy nie wymienia kolekcji ani fokusu.</summary>
public sealed class ContentTimeEnrichment(IContentTimeService? service, TimeProvider? clock = null)
{
    private CancellationTokenSource? _request;
    private int _generation;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private DateTimeOffset? _lastAttempt;
    private bool _manualPending;
    private bool _refreshIntent;
    private Task? _refresh;
    private ContentTimeItemViewModel[] _rows = [];
    public Task Completion { get; private set; } = Task.CompletedTask;

    public void Cancel()
    {
        _generation++;
        _refreshIntent = false;
        _request?.Cancel();
        _request?.Dispose();
        _request = null;
        _rows = [];
    }

    public void Start(IEnumerable<ContentTimeItemViewModel> rows, CancellationToken token = default)
    {
        var carryRefresh = _refreshIntent;
        Cancel();
        _refresh = null;
        _refreshIntent = carryRefresh;
        _rows = rows.Where(r => r.Enabled).ToArray();
        foreach (var row in _rows) row.Apply(row.Raw);
        if (service is null) return;
        _request = CancellationTokenSource.CreateLinkedTokenSource(token);
        _lastAttempt = _clock.GetUtcNow();
        Completion = EnrichAsync(_rows, _generation, _request.Token, carryRefresh, carryRefresh);
    }

    public Task RefreshAsync(IEnumerable<ContentTimeItemViewModel> rows, bool manual = true, CancellationToken token = default)
    {
        if (_refresh is { IsCompleted: false })
        {
            if (manual) _manualPending = true;
            return _refresh;
        }
        if (!manual && _lastAttempt is { } last && _clock.GetUtcNow() - last < TimeSpan.FromMinutes(6))
            return Task.CompletedTask;
        _refresh = RefreshCoreAsync(rows, manual, token);
        return _refresh;
    }

    private async Task RefreshCoreAsync(IEnumerable<ContentTimeItemViewModel> rows, bool manual, CancellationToken token)
    {
        do
        {
            _manualPending = false;
            Cancel();
            _rows = rows.Where(r => r.Enabled).ToArray();
            if (service is null || _rows.Length == 0) return;
            _lastAttempt = _clock.GetUtcNow();
            _request = CancellationTokenSource.CreateLinkedTokenSource(token);
            var generation = _generation;
            _refreshIntent = true;
            Completion = EnrichAsync(_rows, generation, _request.Token, manual, true);
            await Completion;
            if (generation != _generation || token.IsCancellationRequested) return;
            manual = true;
        } while (_manualPending);
    }

    private async Task EnrichAsync(ContentTimeItemViewModel[] rows, int generation, CancellationToken token, bool force = false, bool includeInlineAudio = false)
    {
        try
        {
            var targets = rows.Where(r => includeInlineAudio || r.Key.Source == ContentSource.Article || r.FetchAudio).ToArray();
            var values = force ? await service!.RefreshAsync(targets.Select(r => r.Key), token) :
                await service!.GetAsync(targets.Select(r => r.Key), token);
            if (token.IsCancellationRequested || generation != _generation) return;
            foreach (var row in targets)
                if (values is not ContentTimeBatch status || !status.UnavailableKeys.Contains(row.Key))
                    row.Apply(values.TryGetValue(row.Key, out var value) ? value : null);
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Awaria opcjonalnego dostawcy nie kasuje poprawnych widocznych danych.
        }
        finally
        {
            if (generation == _generation) _refreshIntent = false;
        }
    }
}
