using System.Globalization;
using Fourthwall.Application;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class CanvasGeometryTests
{
    [Fact]
    public void Should_FormatInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // pt-PT uses a comma for the decimal separator; Razor's @double would emit "10,5" there,
        // which is not valid SVG. Invariant must always emit the dot form regardless of thread culture.

        // Arrange
        using var _ = new CulturePin("pt-PT");

        // Act
        var formatted = CanvasGeometry.Invariant(10.5);

        // Assert
        Assert.Equal("10.5", formatted);
    }

    [Fact]
    public void Should_EndAtTheTargetsLeftCentre_When_BuildingAnEdgePath()
    {
        // Arrange
        var from = new ScenePosition(0, 0);
        var to = new ScenePosition(300, 100);

        // Act
        var path = CanvasGeometry.EdgePath(from, to, parallelIndex: 0);

        // Assert
        var expectedEndX = CanvasGeometry.Invariant(to.X);
        var expectedEndY = CanvasGeometry.Invariant(to.Y + (CanvasGeometry.NodeHeight / 2));
        Assert.EndsWith($"{expectedEndX},{expectedEndY}", path, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_OffsetControlPoints_When_ParallelIndexDiffers()
    {
        // Arrange
        var from = new ScenePosition(0, 0);
        var to = new ScenePosition(300, 0);

        // Act
        var straight = CanvasGeometry.EdgePath(from, to, parallelIndex: 0);
        var offset = CanvasGeometry.EdgePath(from, to, parallelIndex: 1);

        // Assert
        Assert.NotEqual(straight, offset);
    }

    [Fact]
    public void Should_FormatSelfLoopPathInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange
        using var _ = new CulturePin("pt-PT");
        var node = new ScenePosition(10.5, 20);

        // Act
        var path = CanvasGeometry.SelfLoopPath(node, parallelIndex: 0);

        // Assert: the loop starts at the node's right-centre (node.X + NodeWidth).
        var expectedStartX = CanvasGeometry.Invariant(node.X + CanvasGeometry.NodeWidth);
        Assert.Contains(expectedStartX, path, StringComparison.Ordinal);
        Assert.DoesNotContain(",", expectedStartX, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_FormatLabelPointInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange
        using var _ = new CulturePin("pt-PT");
        var from = new ScenePosition(10.5, 0);
        var to = new ScenePosition(300, 0);

        // Act
        var (x, y) = CanvasGeometry.LabelPoint(from, to, parallelIndex: 0);

        // Assert
        Assert.DoesNotContain(",", x, StringComparison.Ordinal);
        Assert.DoesNotContain(",", y, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_FormatSelfLoopLabelPointInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange
        using var _ = new CulturePin("pt-PT");
        var node = new ScenePosition(10.5, 20);

        // Act
        var (x, y) = CanvasGeometry.SelfLoopLabelPoint(node, parallelIndex: 0);

        // Assert
        Assert.DoesNotContain(",", x, StringComparison.Ordinal);
        Assert.DoesNotContain(",", y, StringComparison.Ordinal);
    }

    /// <summary>
    /// Swaps <see cref="CultureInfo.CurrentCulture"/> for the lifetime of a test and restores it,
    /// so a culture-sensitive test can never leak into the ones that run after it.
    /// </summary>
    private sealed class CulturePin : IDisposable
    {
        private readonly CultureInfo _original;

        public CulturePin(string name)
        {
            _original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(name);
        }

        public void Dispose() => CultureInfo.CurrentCulture = _original;
    }
}
