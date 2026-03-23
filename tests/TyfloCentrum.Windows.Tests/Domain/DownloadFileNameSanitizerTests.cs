using TyfloCentrum.Windows.Domain.Text;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Domain;

public sealed class DownloadFileNameSanitizerTests
{
    [Fact]
    public void CreateFileName_replaces_invalid_characters_and_keeps_extension()
    {
        var fileName = DownloadFileNameSanitizer.CreateFileName(
            "Podcast: test/odcinek?*",
            "Podcast",
            ".mp3"
        );

        Assert.Equal(".mp3", Path.GetExtension(fileName));
        Assert.DoesNotContain(':', fileName);
        Assert.DoesNotContain('/', fileName);
        Assert.DoesNotContain('?', fileName);
        Assert.DoesNotContain('*', fileName);
    }

    [Fact]
    public void CreateFileName_avoids_reserved_windows_names()
    {
        var fileName = DownloadFileNameSanitizer.CreateFileName("CON", "plik", ".html");

        Assert.NotEqual("CON.html", fileName);
        Assert.EndsWith(".html", fileName);
    }
}
