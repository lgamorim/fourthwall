namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// A rectangle in canvas units, as the edges it reaches: what the canvas's content spans, or what
/// a viewport shows.
/// </summary>
/// <param name="Left">The smallest x the rectangle reaches.</param>
/// <param name="Top">The smallest y the rectangle reaches.</param>
/// <param name="Right">The largest x the rectangle reaches.</param>
/// <param name="Bottom">The largest y the rectangle reaches.</param>
public readonly record struct CanvasBounds(double Left, double Top, double Right, double Bottom)
{
    /// <summary>
    /// Gets bounds that span nothing.
    /// </summary>
    public static CanvasBounds Empty => default;

    /// <summary>
    /// Gets how far the rectangle spans horizontally.
    /// </summary>
    public double Width => Right - Left;

    /// <summary>
    /// Gets how far the rectangle spans vertically.
    /// </summary>
    public double Height => Bottom - Top;

    /// <summary>
    /// Gets whether the rectangle spans no area, as the bounds of an empty story do.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
