using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// The pointer gesture in progress on the canvas: a press on the ground slides the map through the
/// viewport, and a press on a node that travels past <see cref="DragThreshold"/> moves the node.
/// Every coordinate here is a window pixel; node positions come back in canvas units.
/// </summary>
/// <remarks>
/// A press that never travels the threshold is a click, and the browser fires that click after the
/// release; <see cref="ClaimClickAfterDrag"/> lets the component ignore the click that follows a
/// drag, so a drag never selects. The shim forwards every captured move, so moves while idle are
/// ignored rather than rejected.
/// </remarks>
public sealed class CanvasInteraction
{
    /// <summary>
    /// How far, in window pixels, a pressed node must travel before it moves. Under this, the press
    /// is a click.
    /// </summary>
    public const double DragThreshold = 4;

    private readonly CanvasViewport _viewport;
    private double _pressX;
    private double _pressY;
    private double _lastX;
    private double _lastY;
    private ScenePosition _nodeOrigin;
    private ScenePosition _nodePosition;
    private bool _clickAfterDrag;

    /// <summary>
    /// Initializes a new instance of the <see cref="CanvasInteraction"/> class over the viewport a
    /// ground drag slides.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="viewport"/> is <see langword="null"/>.</exception>
    public CanvasInteraction(CanvasViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        _viewport = viewport;
    }

    /// <summary>
    /// Gets what the pointer is doing.
    /// </summary>
    public CanvasInteractionMode Mode { get; private set; }

    /// <summary>
    /// Gets the scene whose node is pressed or being dragged, if any.
    /// </summary>
    public SceneId? PressedScene { get; private set; }

    /// <summary>
    /// Begins a gesture: on the ground when <paramref name="scene"/> is <see langword="null"/>, or
    /// on a node at <paramref name="nodePosition"/>. Ignored while another gesture is in progress.
    /// </summary>
    /// <exception cref="ArgumentException">A node is pressed without its position.</exception>
    public void PointerDown(SceneId? scene, ScenePosition? nodePosition, double screenX, double screenY)
    {
        if (scene is not null && nodePosition is null)
        {
            throw new ArgumentException("A pressed node needs its position.", nameof(nodePosition));
        }

        if (Mode != CanvasInteractionMode.Idle)
        {
            return;
        }

        // A touch drag fires no click, so the mark must not outlive the next press.
        _clickAfterDrag = false;
        PressedScene = scene;
        _pressX = _lastX = screenX;
        _pressY = _lastY = screenY;
        _nodeOrigin = _nodePosition = nodePosition.GetValueOrDefault();
        Mode = scene is null ? CanvasInteractionMode.Panning : CanvasInteractionMode.Pressing;
    }

    /// <summary>
    /// Moves the pointer. Slides the map while panning; returns the node's new position once a
    /// pressed node is being dragged; otherwise <see langword="null"/>.
    /// </summary>
    public NodeMove? PointerMove(double screenX, double screenY)
    {
        switch (Mode)
        {
            case CanvasInteractionMode.Panning:
                _viewport.PanBy(screenX - _lastX, screenY - _lastY);
                _lastX = screenX;
                _lastY = screenY;
                return null;

            case CanvasInteractionMode.Pressing when Travelled(screenX, screenY) < DragThreshold:
                return null;

            case CanvasInteractionMode.Pressing:
            case CanvasInteractionMode.DraggingNode:
                Mode = CanvasInteractionMode.DraggingNode;
                _nodePosition = new ScenePosition(
                    _nodeOrigin.X + ((screenX - _pressX) / _viewport.Scale),
                    _nodeOrigin.Y + ((screenY - _pressY) / _viewport.Scale));
                return new NodeMove(PressedScene.GetValueOrDefault(), _nodePosition);

            default:
                return null;
        }
    }

    /// <summary>
    /// Ends the gesture. Returns the dragged node's final position, or <see langword="null"/> for a
    /// click or a pan.
    /// </summary>
    public NodeMove? PointerUp()
    {
        var result = Mode == CanvasInteractionMode.DraggingNode
            ? new NodeMove(PressedScene.GetValueOrDefault(), _nodePosition)
            : null;

        _clickAfterDrag = result is not null;
        Mode = CanvasInteractionMode.Idle;
        PressedScene = null;
        return result;
    }

    /// <summary>
    /// Reports whether the click the browser fires after the most recent release belongs to a
    /// drag, and forgets it, so it is answered exactly once: that click must not select.
    /// </summary>
    public bool ClaimClickAfterDrag()
    {
        var claimed = _clickAfterDrag;
        _clickAfterDrag = false;
        return claimed;
    }

    private double Travelled(double screenX, double screenY) =>
        Math.Sqrt(Math.Pow(screenX - _pressX, 2) + Math.Pow(screenY - _pressY, 2));
}

/// <summary>
/// A node's new position, in canvas units, as a drag moves it or leaves it.
/// </summary>
/// <param name="Scene">The scene whose node moved.</param>
/// <param name="Position">Where the node's top-left corner now sits.</param>
public sealed record NodeMove(SceneId Scene, ScenePosition Position);
