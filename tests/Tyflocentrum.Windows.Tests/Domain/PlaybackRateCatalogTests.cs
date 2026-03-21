using Tyflocentrum.Windows.Domain.Models;
using Xunit;

namespace Tyflocentrum.Windows.Tests.Domain;

public sealed class PlaybackRateCatalogTests
{
    [Fact]
    public void SupportedValues_expose_quarter_steps_from_one_to_three()
    {
        Assert.Equal(
            [1.0, 1.25, 1.5, 1.75, 2.0, 2.25, 2.5, 2.75, 3.0],
            PlaybackRateCatalog.SupportedValues
        );
    }

    [Theory]
    [InlineData(0.5, 1.0)]
    [InlineData(1.1, 1.0)]
    [InlineData(1.24, 1.25)]
    [InlineData(1.62, 1.5)]
    [InlineData(1.63, 1.75)]
    [InlineData(2.26, 2.25)]
    [InlineData(2.62, 2.5)]
    [InlineData(2.63, 2.75)]
    [InlineData(4.0, 3.0)]
    public void Coerce_returns_nearest_supported_value(double input, double expected)
    {
        Assert.Equal(expected, PlaybackRateCatalog.Coerce(input));
    }
}
