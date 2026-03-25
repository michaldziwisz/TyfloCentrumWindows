using Microsoft.Extensions.Logging;
using System.Text;

namespace TyfloCentrum.Windows.App.Services;

public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private const long MaxFileBytes = 1_500_000;

    private readonly object _gate = new();

    public ILogger CreateLogger(string categoryName)
    {
        return new RollingFileLogger(categoryName, this);
    }

    public void Dispose()
    {
    }

    private void Write(LogLevel logLevel, string categoryName, EventId eventId, string message)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(AppLogFilePaths.DirectoryPath);

                RotateIfNeeded();

                var line =
                    $"{DateTimeOffset.UtcNow:O} [{logLevel}] {categoryName} (EventId: {eventId.Id}) {message}{Environment.NewLine}";
                File.AppendAllText(AppLogFilePaths.CurrentLogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void RotateIfNeeded()
    {
        var currentLogPath = AppLogFilePaths.CurrentLogPath;
        if (!File.Exists(currentLogPath))
        {
            return;
        }

        var currentInfo = new FileInfo(currentLogPath);
        if (currentInfo.Length < MaxFileBytes)
        {
            return;
        }

        var previousLogPath = AppLogFilePaths.PreviousLogPath;
        if (File.Exists(previousLogPath))
        {
            File.Delete(previousLogPath);
        }

        File.Move(currentLogPath, previousLogPath, overwrite: false);
    }

    private sealed class RollingFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly RollingFileLoggerProvider _provider;

        public RollingFileLogger(string categoryName, RollingFileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            if (exception is not null)
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? exception.ToString()
                    : $"{message}{Environment.NewLine}{exception}";
            }

            _provider.Write(logLevel, _categoryName, eventId, message);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
