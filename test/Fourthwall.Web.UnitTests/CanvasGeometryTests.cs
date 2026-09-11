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
    public void Should_OffsetControlPointsLinearlyWithParallelIndex_When_ParallelIndexDiffers()
    {
        // Asserting only that two paths differ would pass for a bug that moves the wrong
        // coordinate, or moves it by the wrong amount. Parsing the control-point Y out of each
        // path pins the actual relationship: consecutive indices are offset by the same amount,
        // and nothing else about the curve (its endpoints) moves.

        // Arrange
        var from = new ScenePosition(0, 0);
        var to = new ScenePosition(300, 0);

        // Act
        var zero = ParseEdgePath(CanvasGeometry.EdgePath(from, to, parallelIndex: 0));
        var one = ParseEdgePath(CanvasGeometry.EdgePath(from, to, parallelIndex: 1));
        var two = ParseEdgePath(CanvasGeometry.EdgePath(from, to, parallelIndex: 2));

        // Assert
        var stepFromZeroToOne = one.ControlOneY - zero.ControlOneY;
        var stepFromOneToTwo = two.ControlOneY - one.ControlOneY;
        Assert.NotEqual(0, stepFromZeroToOne);
        Assert.Equal(stepFromZeroToOne, stepFromOneToTwo);
        Assert.Equal(stepFromZeroToOne, two.ControlTwoY - one.ControlTwoY);
        Assert.Equal(zero.Start, one.Start);
        Assert.Equal(zero.End, one.End);
    }

    [Fact]
    public void Should_FormatSelfLoopPathInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange
        using var _ = new CulturePin("pt-PT");
        var node = new ScenePosition(10.5, 20);

        // Act
        var path = CanvasGeometry.SelfLoopPath(node, parallelIndex: 0);

        // Assert: the loop starts at the node's right-centre (node.X + NodeWidth); this is the
        // only assertion that can fail if SelfLoopPath itself formats with the current culture.
        var expectedStartX = CanvasGeometry.Invariant(node.X + CanvasGeometry.NodeWidth);
        Assert.Contains(expectedStartX, path, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_FormatLabelPointInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Both coordinates are given a fractional part, so a formatter that fell back to the
        // current culture would emit a comma somewhere in the exact string asserted below — under
        // en-US rounding alone ("26") the same test would pass no matter which culture was active.

        // Arrange
        using var _ = new CulturePin("pt-PT");
        var from = new ScenePosition(10.5, 5.5);
        var to = new ScenePosition(300, 5.5);

        // Act
        var (x, y) = CanvasGeometry.LabelPoint(from, to, parallelIndex: 0);

        // Assert
        Assert.Equal("245.25", x);
        Assert.Equal("31.5", y);
    }

    [Fact]
    public void Should_FormatSelfLoopLabelPointInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange
        using var _ = new CulturePin("pt-PT");
        var node = new ScenePosition(10.5, 5.5);

        // Act
        var (x, y) = CanvasGeometry.SelfLoopLabelPoint(node, parallelIndex: 0);

        // Assert
        Assert.Equal("238.5", x);
        Assert.Equal("41.5", y);
    }

    private static (
        (double X, double Y) Start,
        double ControlOneY,
        double ControlTwoY,
        (double X, double Y) End) ParseEdgePath(string path)
    {
        // "M x0,y0 C c1x,c1y c2x,c2y x1,y1" — split on the command letters and the token
        // separators the geometry itself uses, then parse every coordinate with InvariantCulture
        // regardless of what culture produced the string.
        var numbers = path
            .Replace("M ", string.Empty, StringComparison.Ordinal)
            .Replace("C ", string.Empty, StringComparison.Ordinal)
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => double.Parse(token, CultureInfo.InvariantCulture))
            .ToArray();

        return (
            Start: (numbers[0], numbers[1]),
            ControlOneY: numbers[3],
            ControlTwoY: numbers[5],
            End: (numbers[6], numbers[7]));
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
