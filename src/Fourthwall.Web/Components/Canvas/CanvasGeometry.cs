using System.Globalization;
using Fourthwall.Application;

namespace Fourthwall.Web.Components.Canvas;

public static class CanvasGeometry
{
    /// <summary>
    /// The width, in canvas units, of a scene node's bounding box. Provisional until M20's design
    /// plan sets the node's final size.
    /// </summary>
    public const double NodeWidth = 180;

    /// <summary>
    /// The height, in canvas units, of a scene node's bounding box. Provisional until M20's design
    /// plan sets the node's final size.
    /// </summary>
    public const double NodeHeight = 72;

    private const double ParallelGap = 28;
    private const double SelfLoopSize = 48;
    private const double LabelLift = 10;

    /// <summary>
    /// Formats a coordinate for SVG markup independently of the thread's current culture, so a
    /// culture that uses a comma decimal separator (e.g. pt-PT) can never corrupt an attribute.
    /// </summary>
    public static string Invariant(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Builds the path data for a cubic Bézier curve from one node's right-centre to another's
    /// left-centre, with horizontal control handles offset vertically by <paramref name="parallelIndex"/>
    /// so edges sharing the same pair of nodes fan out instead of overlapping.
    /// </summary>
    public static string EdgePath(ScenePosition from, ScenePosition to, int parallelIndex)
    {
        var (startX, startY) = RightCentre(from);
        var (endX, endY) = LeftCentre(to);
        var offset = parallelIndex * ParallelGap;
        var handle = Math.Max(Math.Abs(endX - startX) / 2, NodeWidth / 2);

        var controlOneX = startX + handle;
        var controlOneY = startY + offset;
        var controlTwoX = endX - handle;
        var controlTwoY = endY + offset;

        return $"M {Invariant(startX)},{Invariant(startY)} " +
            $"C {Invariant(controlOneX)},{Invariant(controlOneY)} " +
            $"{Invariant(controlTwoX)},{Invariant(controlTwoY)} " +
            $"{Invariant(endX)},{Invariant(endY)}";
    }

    /// <summary>
    /// Builds the path data for a loop that leaves and re-enters a node's right edge, for a
    /// transition that targets its own scene.
    /// </summary>
    public static string SelfLoopPath(ScenePosition node, int parallelIndex)
    {
        var (x, y) = RightCentre(node);
        var topY = y - (SelfLoopSize / 2);
        var bottomY = y + (SelfLoopSize / 2);
        var loopX = x + SelfLoopSize + (parallelIndex * ParallelGap);

        return $"M {Invariant(x)},{Invariant(topY)} " +
            $"C {Invariant(loopX)},{Invariant(topY)} " +
            $"{Invariant(loopX)},{Invariant(bottomY)} " +
            $"{Invariant(x)},{Invariant(bottomY)}";
    }

    /// <summary>
    /// The point at which an edge's label sits, pre-formatted for SVG markup.
    /// </summary>
    public static (string X, string Y) LabelPoint(ScenePosition from, ScenePosition to, int parallelIndex)
    {
        var (startX, startY) = RightCentre(from);
        var (endX, endY) = LeftCentre(to);
        var offset = parallelIndex * ParallelGap;

        var midX = (startX + endX) / 2;
        var midY = (startY + endY) / 2 + offset - LabelLift;

        return (Invariant(midX), Invariant(midY));
    }

    /// <summary>
    /// The point at which a self-loop's label sits, pre-formatted for SVG markup.
    /// </summary>
    public static (string X, string Y) SelfLoopLabelPoint(ScenePosition node, int parallelIndex)
    {
        var (x, y) = RightCentre(node);
        var labelX = x + SelfLoopSize + (parallelIndex * ParallelGap);

        return (Invariant(labelX), Invariant(y));
    }

    private static (double X, double Y) RightCentre(ScenePosition position) =>
        (position.X + NodeWidth, position.Y + (NodeHeight / 2));

    private static (double X, double Y) LeftCentre(ScenePosition position) =>
        (position.X, position.Y + (NodeHeight / 2));
}
