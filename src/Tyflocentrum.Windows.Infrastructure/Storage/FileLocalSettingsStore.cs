using System.Text.Json;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.Infrastructure.Storage;

public sealed class FileLocalSettingsStore : ILocalSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private Dictionary<string, string>? _entries;

    public FileLocalSettingsStore()
    {
        var rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tyflocentrum.Windows"
        );
        _filePath = Path.Combine(rootPath, "localsettings.json");
    }

    public async ValueTask<string?> GetStringAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries!.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SetStringAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            _entries![key] = value;

            var directoryPath = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, _entries, SerializerOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DeleteStringAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            if (!_entries!.Remove(key))
            {
                return;
            }

            var directoryPath = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, _entries, SerializerOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_entries is not null)
        {
            return;
        }

        if (!File.Exists(_filePath))
        {
            _entries = new Dictionary<string, string>(StringComparer.Ordinal);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _entries =
                await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(
                    stream,
                    SerializerOptions,
                    cancellationToken
                ) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            _entries = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
