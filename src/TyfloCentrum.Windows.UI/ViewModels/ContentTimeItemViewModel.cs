using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Metadata;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class ContentTimeItemViewModel : ObservableObject
{
    private readonly TimeProvider _clock;
    private CancellationTokenSource? _expiry;
    private string? _lastVisible;
    private string? _lastAccessible;
    public ContentTimeItemViewModel(ContentTimeKey key, JsonElement? raw, JsonElement? modified = null,
        bool fetchAudio = false, bool enabled = true, TimeProvider? clock = null)
    {
        Key = key;
        SourceModified = modified;
        FetchAudio = fetchAudio;
        Enabled = enabled;
        _clock = clock ?? TimeProvider.System;
        Apply(raw);
    }

    public ContentTimeKey Key { get; }
    public JsonElement? SourceModified { get; private set; }
    public JsonElement? Raw { get; private set; }
    public bool FetchAudio { get; }
    public bool Enabled { get; }
    public ContentTimeValue? Value => !Enabled ? null : Key.Source == ContentSource.Podcast ?
        ContentTimePolicy.Audio(Raw) : ContentTimePolicy.Reading(Raw, SourceModified, _clock.GetUtcNow());
    public string Visible => Enabled ? ContentTimeFormatter.Visible(Value) : string.Empty;
    public string Accessible => Enabled ? ContentTimeFormatter.Accessible(Value) : string.Empty;

    public void UpdateSource(JsonElement? modified, JsonElement? raw)
    {
        SourceModified = modified;
        // Lista tekstowa zwykle nie niesie czasu; brak pola nie oznacza wycofania.
        // Dla istniejącego audio świeże dane pobiera batch, niezależnie od cache listy.
        Apply(Key.Source == ContentSource.Article ? raw ?? Raw : Raw);
    }

    public void Apply(JsonElement? raw)
    {
        StopExpiry();
        Raw = raw;
        Notify();
        if (Value?.ExpiresAt is not DateTimeOffset expiry) return;
        _expiry = new CancellationTokenSource();
        _ = ExpireAsync(new WeakReference<ContentTimeItemViewModel>(this), expiry, _clock, _expiry.Token);
    }

    public void StopExpiry()
    {
        _expiry?.Cancel();
        _expiry?.Dispose();
        _expiry = null;
    }

    private static async Task ExpireAsync(WeakReference<ContentTimeItemViewModel> target, DateTimeOffset expiry, TimeProvider clock, CancellationToken token)
    {
        try
        {
            var delay = expiry - clock.GetUtcNow();
            if (delay > TimeSpan.Zero) await Task.Delay(delay, clock, token);
            if (!token.IsCancellationRequested && target.TryGetTarget(out var row)) row.Notify();
        }
        catch (OperationCanceledException) { }
    }

    private void Notify()
    {
        var visible = Visible;
        var accessible = Accessible;
        if (_lastVisible != visible)
        {
            _lastVisible = visible;
            OnPropertyChanged(nameof(Visible));
        }
        if (_lastAccessible != accessible)
        {
            _lastAccessible = accessible;
            OnPropertyChanged(nameof(Accessible));
        }
    }
}
