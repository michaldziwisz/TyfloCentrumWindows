namespace TyfloCentrum.Windows.App.Services;

internal static class InternalStoreScreenshotRequestParser
{
    private const string ScreenshotFlag = "--internal-store-screenshot";
    private const string OutputFlag = "--internal-store-screenshot-output";

    public static InternalStoreScreenshotRequest? Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return null;
        }

        string? sectionKey = null;
        string? outputPath = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            if (TryReadArgumentValue(arguments, ref index, argument, ScreenshotFlag, out var screenshot))
            {
                sectionKey = screenshot;
                continue;
            }

            if (TryReadArgumentValue(arguments, ref index, argument, OutputFlag, out var output))
            {
                outputPath = output;
            }
        }

        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(Path.GetTempPath(), $"TyfloCentrum-{sectionKey}.png");
        }

        return new InternalStoreScreenshotRequest(sectionKey.Trim(), outputPath.Trim());
    }

    private static bool TryReadArgumentValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string argument,
        string flag,
        out string value
    )
    {
        if (argument.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = argument[(flag.Length + 1)..];
            return true;
        }

        if (
            string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase)
            && index + 1 < arguments.Count
        )
        {
            index++;
            value = arguments[index];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
