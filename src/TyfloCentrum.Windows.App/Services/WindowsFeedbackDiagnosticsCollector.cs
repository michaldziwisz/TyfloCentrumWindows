using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using Windows.ApplicationModel;
using Windows.System.Profile;

namespace TyfloCentrum.Windows.App.Services;

public sealed class WindowsFeedbackDiagnosticsCollector : IFeedbackDiagnosticsCollector
{
    private const int MaxFullLogBytes = 1_000_000;
    private const int MaxTailLogBytes = 300_000;
    private const int MaxBase64Length = 8_000_000;

    public Task<FeedbackDiagnosticsSnapshot> CollectAsync(
        bool includeDiagnostics,
        bool includeLogFile,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = GetPackageVersion();
        var diagnostics = includeDiagnostics ? BuildSafeDiagnostics(version) : EmptyDiagnostics;
        var logAttachment = includeLogFile ? TryPrepareLogAttachment(cancellationToken) : null;

        return Task.FromResult(
            new FeedbackDiagnosticsSnapshot(
                version,
                version,
                "stable",
                $"TyfloCentrum.Windows.App/{version}",
                diagnostics,
                logAttachment
            )
        );
    }

    private static FeedbackLogAttachment? TryPrepareLogAttachment(CancellationToken cancellationToken)
    {
        var path = AppLogFilePaths.CurrentLogPath;
        if (!File.Exists(path))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var info = new FileInfo(path);
        var originalBytes = info.Length > int.MaxValue ? int.MaxValue : (int)info.Length;
        var bytes = ReadLogBytes(path, info.Length, out var truncated);
        if (bytes.Length == 0)
        {
            return null;
        }

        var compressed = Compress(bytes);
        var base64 = Convert.ToBase64String(compressed);

        if (base64.Length > MaxBase64Length)
        {
            var reduced = bytes.Length > MaxTailLogBytes / 2
                ? bytes[^Math.Min(bytes.Length, MaxTailLogBytes / 2)..]
                : bytes;
            compressed = Compress(reduced);
            base64 = Convert.ToBase64String(compressed);
            truncated = true;
        }

        return new FeedbackLogAttachment(
            "tyflocentrum-current.log.gz",
            "application/gzip",
            "base64",
            base64,
            originalBytes,
            truncated
        );
    }

    private static byte[] ReadLogBytes(string path, long length, out bool truncated)
    {
        truncated = false;
        if (length <= MaxFullLogBytes)
        {
            return File.ReadAllBytes(path);
        }

        truncated = true;
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytesToRead = (int)Math.Min(length, MaxTailLogBytes);
        stream.Seek(-bytesToRead, SeekOrigin.End);
        var buffer = new byte[bytesToRead];
        var read = stream.Read(buffer, 0, buffer.Length);
        return read == buffer.Length ? buffer : buffer[..read];
    }

    private static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    private static IReadOnlyDictionary<string, object?> BuildSafeDiagnostics(string version)
    {
        return new Dictionary<string, object?>
        {
            ["app"] = new Dictionary<string, object?>
            {
                ["name"] = "TyfloCentrum",
                ["package"] = "MichaDziwisz.TyfloCentrum",
                ["version"] = version,
                ["channel"] = "stable",
                ["distribution"] = "msix",
                ["dependency"] = ".NET Desktop Runtime",
            },
            ["os"] = new Dictionary<string, object?>
            {
                ["system"] = "Windows",
                ["version"] = GetWindowsVersion(),
                ["architecture"] = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                ["uiLanguage"] = CultureInfo.CurrentUICulture.Name,
            },
            ["runtime"] = new Dictionary<string, object?>
            {
                ["framework"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                ["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            },
        };
    }

    private static string GetPackageVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            var assemblyVersion = typeof(App).Assembly.GetName().Version;
            return assemblyVersion?.ToString() ?? "0.0.0.0";
        }
    }

    private static string GetWindowsVersion()
    {
        try
        {
            var version = ulong.Parse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion, CultureInfo.InvariantCulture);
            var major = (version & 0xFFFF000000000000L) >> 48;
            var minor = (version & 0x0000FFFF00000000L) >> 32;
            var build = (version & 0x00000000FFFF0000L) >> 16;
            var revision = version & 0x000000000000FFFFL;
            return $"{major}.{minor}.{build}.{revision}";
        }
        catch
        {
            return Environment.OSVersion.VersionString;
        }
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyDiagnostics =
        new Dictionary<string, object?>();
}
