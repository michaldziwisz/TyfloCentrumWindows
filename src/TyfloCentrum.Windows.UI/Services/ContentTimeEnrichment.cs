using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.UI.Services;

/// <summary>Uzupełnia istniejące wiersze, nigdy nie wymienia kolekcji ani fokusu.</summary>
public sealed class ContentTimeEnrichment(IContentTimeService? service)
{
    private CancellationTokenSource? _request;
    private int _generation;
    private ContentTimeItemViewModel[] _rows = [];
    public Task Completion { get; private set; } = Task.CompletedTask;

    public void Cancel()
    {
        _generation++;
        _request?.Cancel();
        _request?.Dispose();
        _request = null;
        _rows = [];
    }

    public void Start(IEnumerable<ContentTimeItemViewModel> rows, CancellationToken token = default)
    {
        Cancel();
        _rows = rows.Where(r => r.Enabled).ToArray();
        foreach (var row in _rows) row.Apply(row.Raw);
        if (service is null) return;
        _request = CancellationTokenSource.CreateLinkedTokenSource(token);
        Completion = EnrichAsync(_rows, _generation, _request.Token);
    }

    private async Task EnrichAsync(ContentTimeItemViewModel[] rows, int generation, CancellationToken token)
    {
        try
        {
            var targets = rows.Where(r => r.Key.Source == ContentSource.Article || r.FetchAudio).ToArray();
            var values = await service!.GetAsync(targets.Select(r => r.Key), token);
            if (token.IsCancellationRequested || generation != _generation) return;
            foreach (var row in targets)
                row.Apply(values.TryGetValue(row.Key, out var value) ? value : null);
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Opcjonalny dostawca nie może zmienić stanu poprawnej listy na błąd.
            if (!token.IsCancellationRequested && generation == _generation)
                foreach (var row in rows.Where(r => r.Key.Source == ContentSource.Article || r.FetchAudio)) row.Apply(null);
        }
    }
}
