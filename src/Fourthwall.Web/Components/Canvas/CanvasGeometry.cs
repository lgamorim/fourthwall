using System.Globalization;
using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// Every size and shape the canvas draws, in canvas units, with the SVG path data built from them.
/// </summary>
/// <remarks>
/// The values come from the visual direction's canvas section (docs/design/0002-visual-direction.md
/// §10): the node is a page whose right edge is the shape of its kind.
/// </remarks>
public static class CanvasGeometry
{
    /// <summary>
    /// The width, in canvas units, of a scene node's bounding box.
    /// </summary>
    public const double NodeWidth = 200;

    /// <summary>
    /// The height, in canvas units, of a scene node's bounding box.
    /// </summary>
    public const double NodeHeight = 64;

    /// <summary>
    /// How far a Linear node's point reaches, and a Choice node's notch cuts, into the right edge.
    /// </summary>
    public const double ExitDepth = 14;

    /// <summary>
    /// The side of a node's square image thumbnail.
    /// </summary>
    public const double ThumbnailSize = 40;

    /// <summary>
    /// The thumbnail's left edge: inside the right edge, clear of the point or notch.
    /// </summary>
    public const double ThumbnailX = NodeWidth - ExitDepth - 8 - ThumbnailSize;

    /// <summary>
    /// The thumbnail's top edge, centring it on the node's height.
    /// </summary>
    public const double ThumbnailY = (NodeHeight - ThumbnailSize) / 2;

    /// <summary>
    /// The room left past the furthest node or self-loop when sizing the drawing — the same margin
    /// the auto-layout leaves at the origin.
    /// </summary>
    public const double ContentMargin = 40;

    /// <summary>
    /// The ribbon bookmark's width. Mirrors the <c>--fw-ribbon-width</c> token, which SVG path data
    /// cannot read.
    /// </summary>
    public const double RibbonWidth = 8;

    /// <summary>
    /// The ribbon bookmark's length. Mirrors the <c>--fw-ribbon-length</c> token, which SVG path data
    /// cannot read.
    /// </summary>
    public const double RibbonLength = 22;

    /// <summary>
    /// The radius of a node's left corners and of its thumbnail plate. Mirrors the
    /// <c>--fw-radius-s</c> token, which SVG path data and geometry attributes cannot read.
    /// </summary>
    public const double CornerRadius = 2;

    /// <summary>
    /// The radius of the ring drawn for a node's port at rest.
    /// </summary>
    public const double PortRadius = 4;

    /// <summary>
    /// The radius of the invisible circle that takes a press on a node's port, larger than the
    /// ring so the port is easy to catch.
    /// </summary>
    public const double PortHitRadius = 10;

    private const double RibbonInset = 4;
    private const double RibbonNotch = 0.7;
    private const double ParallelGap = 28;
    private const double LabelLift = 10;

    // A self-loop's feet stand on the top edge, the right foot this far in from the right edge;
    // each further loop on the scene rises higher and spreads its feet wider.
    private const double SelfLoopInset = 44;
    private const double SelfLoopWidth = 40;
    private const double SelfLoopSize = 40;
    private const double SelfLoopStep = 24;
    private const double SelfLoopSpread = 8;
    private const double SelfLoopLabelGap = 6;
    private const double SelfLoopLabelDrop = 4;

    // A cut link label (16 characters of the utility face's smallest size) with room to spare.
    private const double SelfLoopLabelRoom = 120;

    /// <summary>
    /// How far a self-loop's label reaches past its node's right edge, so the drawing can be sized
    /// to show it.
    /// </summary>
    /// <param name="parallelIndex">The loop's position among the node's other self-loops.</param>
    public static double SelfLoopReach(int parallelIndex) =>
        (parallelIndex * SelfLoopSpread) - SelfLoopInset + SelfLoopLabelGap + SelfLoopLabelRoom;

    /// <summary>
    /// The path data for the ribbon bookmark that hangs from the selected node's top edge, ending in
    /// the same fishtail notch as the navigator's ribbon.
    /// </summary>
    public static string RibbonPath { get; } =
        $"M {Invariant(RibbonInset)},0 " +
        $"H {Invariant(RibbonInset + RibbonWidth)} " +
        $"V {Invariant(RibbonLength)} " +
        $"L {Invariant(RibbonInset + (RibbonWidth / 2))},{Invariant(RibbonLength * RibbonNotch)} " +
        $"L {Invariant(RibbonInset)},{Invariant(RibbonLength)} Z";

    /// <summary>
    /// Builds the outline of a node of the given kind, relative to the node's top-left corner: a
    /// straight left edge where links enter, and a right edge shaped like the kind's mark.
    /// </summary>
    /// <param name="kind">The scene kind to outline.</param>
    /// <returns>SVG path data, formatted independently of the current culture.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a known kind.</exception>
    public static string NodeOutline(SceneKind kind)
    {
        var middle = NodeHeight / 2;
        var rightEdge = kind switch
        {
            // The arrow: one way on, leaving from the point.
            SceneKind.Linear =>
                $"H {Invariant(NodeWidth - ExitDepth)} L {Invariant(NodeWidth)},{Invariant(middle)} " +
                $"L {Invariant(NodeWidth - ExitDepth)},{Invariant(NodeHeight)} ",

            // The fork: the edge cuts in to a crook the links fan out from.
            SceneKind.Choice =>
                $"H {Invariant(NodeWidth)} L {Invariant(NodeWidth - ExitDepth)},{Invariant(middle)} " +
                $"L {Invariant(NodeWidth)},{Invariant(NodeHeight)} ",

            // The full stop: a half-circle as tall as the node.
            SceneKind.Ending =>
                $"H {Invariant(NodeWidth - middle)} " +
                $"A {Invariant(middle)},{Invariant(middle)} 0 0 1 {Invariant(NodeWidth - middle)},{Invariant(NodeHeight)} ",

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scene kind."),
        };

        var corner = Invariant(CornerRadius);
        return $"M {corner},0 " +
            rightEdge +
            $"H {corner} " +
            $"A {corner},{corner} 0 0 1 0,{Invariant(NodeHeight - CornerRadius)} " +
            $"V {corner} " +
            $"A {corner},{corner} 0 0 1 {corner},0 Z";
    }

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
        var curve = EdgeCurve(from, to, parallelIndex);

        return $"M {Invariant(curve.StartX)},{Invariant(curve.StartY)} " +
            $"C {Invariant(curve.ControlOneX)},{Invariant(curve.ControlOneY)} " +
            $"{Invariant(curve.ControlTwoX)},{Invariant(curve.ControlTwoY)} " +
            $"{Invariant(curve.EndX)},{Invariant(curve.EndY)}";
    }

    /// <summary>
    /// Where a node's port sits, in canvas units: its right centre, where every link leaves.
    /// </summary>
    public static ScenePosition PortCentre(ScenePosition node)
    {
        var (x, y) = RightCentre(node);
        return new ScenePosition(x, y);
    }

    /// <summary>
    /// Builds the path data for a link being drawn from a node to a point on the paper: the curve a
    /// link to a scene whose left centre sat at <paramref name="end"/> would take.
    /// </summary>
    public static string DraftPath(ScenePosition from, ScenePosition end) =>
        EdgePath(from, new ScenePosition(end.X, end.Y - (NodeHeight / 2)), parallelIndex: 0);

    /// <summary>
    /// The exact box the curve of <see cref="EdgePath"/> reaches. A link that doubles back bows
    /// past both of its ends, and the drawing's bounds must hold the bow to show the whole story.
    /// </summary>
    public static CanvasBounds EdgeExtent(ScenePosition from, ScenePosition to, int parallelIndex)
    {
        var curve = EdgeCurve(from, to, parallelIndex);
        var (left, right) = CubicExtent(curve.StartX, curve.ControlOneX, curve.ControlTwoX, curve.EndX);
        var (top, bottom) = CubicExtent(curve.StartY, curve.ControlOneY, curve.ControlTwoY, curve.EndY);
        return new CanvasBounds(left, top, right, bottom);
    }

    /// <summary>
    /// Builds the path data for a loop that rises from a node's top edge and comes back down into
    /// it, for a transition that targets its own scene. The loop sits right of the start tag and
    /// the ribbon, away from the right edge that every other outgoing link leaves from; a second
    /// loop on the same scene rises higher and spans wider.
    /// </summary>
    public static string SelfLoopPath(ScenePosition node, int parallelIndex)
    {
        var (startX, endX) = SelfLoopFeet(node, parallelIndex);
        var controlY = node.Y - SelfLoopRise(parallelIndex);

        return $"M {Invariant(startX)},{Invariant(node.Y)} " +
            $"C {Invariant(startX)},{Invariant(controlY)} " +
            $"{Invariant(endX)},{Invariant(controlY)} " +
            $"{Invariant(endX)},{Invariant(node.Y)}";
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
        var (startX, _) = SelfLoopFeet(node, parallelIndex);

        // A cubic's apex sits three quarters of the way to its control points; the label's
        // baseline sits just under it, so the text reads beside the top of the loop.
        var labelY = node.Y - (SelfLoopRise(parallelIndex) * 0.75) + SelfLoopLabelDrop;

        return (Invariant(startX + SelfLoopLabelGap), Invariant(labelY));
    }

    /// <summary>
    /// How far above its node's top edge a self-loop's control points rise, so the drawing can be
    /// bounded to hold it; each further loop on the same scene rises higher.
    /// </summary>
    /// <param name="parallelIndex">The loop's position among the node's other self-loops.</param>
    public static double SelfLoopRise(int parallelIndex) => SelfLoopSize + (parallelIndex * SelfLoopStep);

    // The cubic from one node's right-centre to another's left-centre, with horizontal handles
    // offset vertically by the parallel index so links sharing a pair of nodes fan out.
    private static EdgeCurve EdgeCurve(ScenePosition from, ScenePosition to, int parallelIndex)
    {
        var (startX, startY) = RightCentre(from);
        var (endX, endY) = LeftCentre(to);
        var offset = parallelIndex * ParallelGap;
        var handle = Math.Max(Math.Abs(endX - startX) / 2, NodeWidth / 2);

        return new EdgeCurve(
            startX, startY, startX + handle, startY + offset, endX - handle, endY + offset, endX, endY);
    }

    // One axis of a cubic Bézier reaches its extremes at an end, or where its derivative — a
    // quadratic in t — is zero inside (0, 1).
    private static (double Min, double Max) CubicExtent(double p0, double p1, double p2, double p3)
    {
        var min = Math.Min(p0, p3);
        var max = Math.Max(p0, p3);

        var u = p1 - p0;
        var v = p2 - p1;
        var w = p3 - p2;
        var a = u - (2 * v) + w;
        var b = 2 * (v - u);
        var c = u;

        foreach (var t in QuadraticRoots(a, b, c))
        {
            if (t > 0 && t < 1)
            {
                var s = 1 - t;
                var value = (s * s * s * p0) + (3 * s * s * t * p1) + (3 * s * t * t * p2) + (t * t * t * p3);
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
        }

        return (min, max);
    }

    private static IEnumerable<double> QuadraticRoots(double a, double b, double c)
    {
        const double Flat = 1e-9;

        if (Math.Abs(a) < Flat)
        {
            if (Math.Abs(b) >= Flat)
            {
                yield return -c / b;
            }

            yield break;
        }

        var discriminant = (b * b) - (4 * a * c);
        if (discriminant < 0)
        {
            yield break;
        }

        var root = Math.Sqrt(discriminant);
        yield return (-b + root) / (2 * a);
        yield return (-b - root) / (2 * a);
    }

    private static (double StartX, double EndX) SelfLoopFeet(ScenePosition node, int parallelIndex)
    {
        var spread = parallelIndex * SelfLoopSpread;
        var startX = node.X + NodeWidth - SelfLoopInset + spread;
        return (startX, startX - SelfLoopWidth - (2 * spread));
    }


    private static (double X, double Y) RightCentre(ScenePosition position) =>
        (position.X + NodeWidth, position.Y + (NodeHeight / 2));

    private static (double X, double Y) LeftCentre(ScenePosition position) =>
        (position.X, position.Y + (NodeHeight / 2));
}

/// <summary>
/// The control points of a link's cubic curve, before formatting.
/// </summary>
internal readonly record struct EdgeCurve(
    double StartX,
    double StartY,
    double ControlOneX,
    double ControlOneY,
    double ControlTwoX,
    double ControlTwoY,
    double EndX,
    double EndY);
