namespace TyfloCentrum.Windows.App.Services;

internal static class AppLogFilePaths
{
    private static readonly string RootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TyfloCentrum.Windows",
        "logs"
    );

    public static string DirectoryPath => RootPath;

    public static string CurrentLogPath => Path.Combine(RootPath, "current.log");

    public static string PreviousLogPath => Path.Combine(RootPath, "previous.log");
}
