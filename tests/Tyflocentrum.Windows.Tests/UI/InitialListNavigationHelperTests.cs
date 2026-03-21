using Tyflocentrum.Windows.UI.Services;
using Xunit;

namespace Tyflocentrum.Windows.Tests.UI;

public sealed class InitialListNavigationHelperTests
{
    [Fact]
    public void FindNextByInitial_matches_by_first_letter_and_wraps()
    {
        var items = new[] { "Nowości", "Podcasty", "Artykuły", "Szukaj" };

        var match = InitialListNavigationHelper.FindNextByInitial(
            items,
            0,
            item => item,
            'P'
        );

        Assert.Equal("Podcasty", match);
    }

    [Fact]
    public void FindNextByInitial_ignores_polish_diacritics()
    {
        var items = new[] { "Aktualności", "Łącza", "Świat", "Żródła" };

        var match = InitialListNavigationHelper.FindNextByInitial(
            items,
            0,
            item => item,
            'S'
        );

        Assert.Equal("Świat", match);
    }
}
