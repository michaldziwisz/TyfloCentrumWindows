using System.Xml.Linq;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ContentTimeScrollLayoutTests
{
    [Theory]
    [InlineData("SearchSectionView", "ResultsList")]
    [InlineData("FavoritesSectionView", "ItemsList")]
    [InlineData("ArticleSectionView", "MagazineContentHost")]
    public void RefreshableLongListHasBoundedViewport(string view, string listName)
    {
        // Ochrona przyczyny natywnego RED: StackPanel dawał listom 17120/48480 px,
        // więc ScrollViewer nie miał żadnego zakresu i wiersze znikały pod oknem.
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", view + ".xaml"));
        var list = Assert.Single(document.Descendants().Where(e => (string?)e.Attribute(x + "Name") == listName));
        var grid = list.Parent!;
        Assert.Equal("Grid", grid.Name.LocalName);
        var row = int.Parse((string?)list.Attribute("Grid.Row") ?? "0");
        var definitions = grid.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions").Elements().ToArray();
        Assert.Equal("*", (string?)definitions[row].Attribute("Height"));
        Assert.DoesNotContain(grid.Ancestors(), e => e.Name.LocalName == "StackPanel" && (string?)e.Attribute("Orientation") != "Horizontal");
    }
}
