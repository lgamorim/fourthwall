using System.Globalization;
using Fourthwall.Application;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// The window onto the map: where the map's origin sits in the window and how far it is zoomed,
/// with the conversions between window pixels and canvas units that pan, zoom, drag, and framing
/// need. Nothing here touches the DOM; the component renders <see cref="Transform"/>.
/// </summary>
/// <remarks>
/// A window point is <c>world × Scale + Translate</c>. The viewport size is zero until the browser
/// measures it, and framing (<see cref="Fit"/>, <see cref="CentreOn"/>, <see cref="ZoomBy"/>) needs
/// that size, so those wait for it. Behaviour follows docs/design/0002-visual-direction.md §11.
/// </remarks>
public sealed class CanvasViewport
{
    /// <summary>
    /// The furthest the map zooms out: a quarter of actual size.
    /// </summary>
    public const double MinScale = 0.25;

    /// <summary>
    /// The furthest the map zooms in: three times actual size.
    /// </summary>
    public const double MaxScale = 3;

    /// <summary>
    /// How much one zoom step scales the map: one wheel notch, or one key press.
    /// </summary>
    public const double ZoomStep = 1.2;

    /// <summary>
    /// Gets how far the map is zoomed: canvas units to window pixels.
    /// </summary>
    public double Scale { get; private set; } = 1;

    /// <summary>
    /// Gets where the map's origin sits, in window pixels from the window's left edge.
    /// </summary>
    public double TranslateX { get; private set; }

    /// <summary>
    /// Gets where the map's origin sits, in window pixels from the window's top edge.
    /// </summary>
    public double TranslateY { get; private set; }

    /// <summary>
    /// Gets the window's width in pixels; zero until measured.
    /// </summary>
    public double Width { get; private set; }

    /// <summary>
    /// Gets the window's height in pixels; zero until measured.
    /// </summary>
    public double Height { get; private set; }

    /// <summary>
    /// Gets whether the browser has reported the window's size yet.
    /// </summary>
    public bool IsMeasured => Width > 0 && Height > 0;

    /// <summary>
    /// Gets the SVG transform that places the map in the window, formatted independently of the
    /// current culture. The scale carries four decimals: two would draw a point a thousand units
    /// from the origin several pixels away from where the maths keeps it.
    /// </summary>
    public string Transform =>
        $"translate({CanvasGeometry.Invariant(TranslateX)} {CanvasGeometry.Invariant(TranslateY)}) " +
        $"scale({Scale.ToString("0.####", CultureInfo.InvariantCulture)})";

    /// <summary>
    /// Records the window's size, as the browser measures it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Either side is negative.</exception>
    public void SetSize(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Converts a window point to the canvas point drawn there.
    /// </summary>
    public ScenePosition ToWorld(double screenX, double screenY) =>
        new((screenX - TranslateX) / Scale, (screenY - TranslateY) / Scale);

    /// <summary>
    /// Converts a canvas point to where the window draws it.
    /// </summary>
    public (double X, double Y) ToScreen(ScenePosition world) =>
        ((world.X * Scale) + TranslateX, (world.Y * Scale) + TranslateY);

    /// <summary>
    /// Zooms by a number of steps (positive in, negative out, fractional for a wheel's partial
    /// notch), keeping the canvas point under the given window point where it is. The scale stops
    /// at <see cref="MinScale"/> and <see cref="MaxScale"/>.
    /// </summary>
    public void ZoomAt(double screenX, double screenY, double steps)
    {
        var anchor = ToWorld(screenX, screenY);
        Scale = Math.Clamp(Scale * Math.Pow(ZoomStep, steps), MinScale, MaxScale);
        TranslateX = screenX - (anchor.X * Scale);
        TranslateY = screenY - (anchor.Y * Scale);
    }

    /// <summary>
    /// Zooms by a number of steps about the window's centre, for a keyboard press.
    /// </summary>
    public void ZoomBy(double steps) => ZoomAt(Width / 2, Height / 2, steps);

    /// <summary>
    /// Slides the map by a window-pixel delta, whatever the zoom.
    /// </summary>
    public void PanBy(double deltaX, double deltaY)
    {
        TranslateX += deltaX;
        TranslateY += deltaY;
    }

    /// <summary>
    /// Frames the given content in the window: centred, with <paramref name="padding"/> pixels of
    /// room on every side, zoomed out as far as needed and never past actual size. Empty bounds
    /// return the map to actual size at the origin; an unmeasured window leaves the view alone.
    /// </summary>
    public void Fit(CanvasBounds bounds, double padding)
    {
        if (!IsMeasured)
        {
            return;
        }

        if (bounds.IsEmpty)
        {
            Reset();
            return;
        }

        var scale = Math.Min(1, Math.Min((Width - (2 * padding)) / bounds.Width, (Height - (2 * padding)) / bounds.Height));
        Scale = Math.Clamp(scale, MinScale, MaxScale);
        TranslateX = ((Width - (bounds.Width * Scale)) / 2) - (bounds.Left * Scale);
        TranslateY = ((Height - (bounds.Height * Scale)) / 2) - (bounds.Top * Scale);
    }

    /// <summary>
    /// Returns the map to actual size with its origin at the window's top-left corner.
    /// </summary>
    public void Reset()
    {
        Scale = 1;
        TranslateX = 0;
        TranslateY = 0;
    }

    /// <summary>
    /// Slides the map so the given canvas point sits at the window's centre, keeping the zoom.
    /// </summary>
    public void CentreOn(ScenePosition world)
    {
        TranslateX = (Width / 2) - (world.X * Scale);
        TranslateY = (Height / 2) - (world.Y * Scale);
    }

    /// <summary>
    /// Determines whether the whole of the given content lies inside the measured window.
    /// </summary>
    public bool Shows(CanvasBounds bounds)
    {
        if (!IsMeasured)
        {
            return false;
        }

        var (left, top) = ToScreen(new ScenePosition(bounds.Left, bounds.Top));
        var (right, bottom) = ToScreen(new ScenePosition(bounds.Right, bounds.Bottom));
        return left >= 0 && top >= 0 && right <= Width && bottom <= Height;
    }
}
