using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class CanvasBoundsTests
{
    [Fact]
    public void Should_MeasureWidthAndHeight_When_Built()
    {
        // Arrange
        var bounds = new CanvasBounds(Left: -10, Top: 5, Right: 30, Bottom: 25.5);

        // Act & Assert
        Assert.Equal(40, bounds.Width);
        Assert.Equal(20.5, bounds.Height);
        Assert.False(bounds.IsEmpty);
    }

    [Fact]
    public void Should_BeEmpty_When_NothingWasMeasured()
    {
        // Arrange & Act
        var bounds = CanvasBounds.Empty;

        // Assert
        Assert.True(bounds.IsEmpty);
    }

    [Theory]
    [InlineData(0, 0, 0, 10)]
    [InlineData(0, 0, 10, 0)]
    public void Should_BeEmpty_When_EitherSideHasNoExtent(double left, double top, double right, double bottom)
    {
        // Arrange & Act
        var bounds = new CanvasBounds(left, top, right, bottom);

        // Assert
        Assert.True(bounds.IsEmpty);
    }
}
