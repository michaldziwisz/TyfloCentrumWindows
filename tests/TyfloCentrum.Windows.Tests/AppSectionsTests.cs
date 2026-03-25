using TyfloCentrum.Windows.Domain.Catalog;
using Xunit;

namespace TyfloCentrum.Windows.Tests;

public sealed class AppSectionsTests
{
    [Fact]
    public void All_contains_expected_primary_sections()
    {
        var sections = AppSections.All;

        Assert.Collection(
            sections,
            item =>
            {
                Assert.Equal("news", item.Key);
                Assert.Equal(1, item.ShortcutNumber);
                Assert.Equal("Alt+1", item.ShortcutLabel);
            },
            item =>
            {
                Assert.Equal("podcasts", item.Key);
                Assert.Equal(2, item.ShortcutNumber);
                Assert.Equal("Alt+2", item.ShortcutLabel);
            },
            item =>
            {
                Assert.Equal("articles", item.Key);
                Assert.Equal(3, item.ShortcutNumber);
                Assert.Equal("Alt+3", item.ShortcutLabel);
            },
            item =>
            {
                Assert.Equal("search", item.Key);
                Assert.Equal(4, item.ShortcutNumber);
                Assert.Equal("Alt+4", item.ShortcutLabel);
            },
            item =>
            {
                Assert.Equal("favorites", item.Key);
                Assert.Equal(5, item.ShortcutNumber);
                Assert.Equal("Alt+5", item.ShortcutLabel);
            },
            item =>
            {
                Assert.Equal("radio", item.Key);
                Assert.Equal(6, item.ShortcutNumber);
                Assert.Equal("Alt+6", item.ShortcutLabel);
            },
            item =>
            {
                Assert.Equal("settings", item.Key);
                Assert.Equal(7, item.ShortcutNumber);
                Assert.Equal("Alt+7", item.ShortcutLabel);
            },
            item =>
            {
                Assert.Equal("feedback", item.Key);
                Assert.Equal(8, item.ShortcutNumber);
                Assert.Equal("Alt+8", item.ShortcutLabel);
            }
        );
    }
}
