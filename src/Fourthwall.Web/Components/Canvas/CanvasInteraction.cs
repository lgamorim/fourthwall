using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// The pointer gesture in progress on the canvas: a press on the ground slides the map through the
/// viewport, a press on a node that travels past <see cref="DragThreshold"/> moves the node, and a
/// press on a node's port draws a link to the scene it is dropped on. Every coordinate here is a
/// window pixel; node positions and the draft's end come back in canvas units.
/// </summary>
/// <remarks>
/// A press that never travels the threshold is a click, and the browser fires that click after the
/// release; <see cref="ClaimClickAfterDrag"/> lets the component ignore the click that follows a
/// drag or a pan, so moving a node never selects it and sliding the map from a link never selects
/// the link. The shim forwards every captured move, so moves while idle are ignored rather than
/// rejected.
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
    private bool _panTravelled;
    private bool _clickAfterDrag;
    private Func<ScenePosition, SceneId?>? _hitTest;

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
    /// Gets the scene whose node is pressed or being dragged, or whose port a link is drawn from.
    /// </summary>
    public SceneId? PressedScene { get; private set; }

    /// <summary>
    /// Gets where the draft link ends, in canvas units, while one is drawn.
    /// </summary>
    public ScenePosition? DraftEnd { get; private set; }

    /// <summary>
    /// Gets the scene the draft link would link to if released now: the scene under its end, never
    /// the scene it is drawn from.
    /// </summary>
    public SceneId? DropTarget { get; private set; }

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

        if (!Begin(scene, screenX, screenY))
        {
            return;
        }

        _nodeOrigin = _nodePosition = nodePosition.GetValueOrDefault();
        Mode = scene is null ? CanvasInteractionMode.Panning : CanvasInteractionMode.Pressing;
    }

    /// <summary>
    /// Begins drawing a link from a scene's port. The draft's end starts at the port and follows the
    /// pointer; <paramref name="hitTest"/> names the scene under a canvas point, and is asked again
    /// on every move and on the release, so it should read the map as it is drawn now. Ignored
    /// while another gesture is in progress.
    /// </summary>
    /// <param name="source">The scene the link leaves.</param>
    /// <param name="port">The port's centre, in canvas units.</param>
    /// <param name="screenX">The press's window x.</param>
    /// <param name="screenY">The press's window y.</param>
    /// <param name="hitTest">Finds the scene under a canvas point, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hitTest"/> is <see langword="null"/>.</exception>
    public void PortDown(
        SceneId source, ScenePosition port, double screenX, double screenY, Func<ScenePosition, SceneId?> hitTest)
    {
        ArgumentNullException.ThrowIfNull(hitTest);

        if (!Begin(source, screenX, screenY))
        {
            return;
        }

        _hitTest = hitTest;
        _nodeOrigin = port;
        DraftEnd = port;
        DropTarget = null;
        Mode = CanvasInteractionMode.DrawingEdge;
    }

    /// <summary>
    /// Moves the pointer. Slides the map while panning; returns the node's new position once a
    /// pressed node is being dragged; moves the draft's end while a link is drawn; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public NodeMove? PointerMove(double screenX, double screenY)
    {
        switch (Mode)
        {
            case CanvasInteractionMode.Panning:
                _viewport.PanBy(screenX - _lastX, screenY - _lastY);
                _lastX = screenX;
                _lastY = screenY;
                _panTravelled |= Travelled(screenX, screenY) >= DragThreshold;
                return null;

            case CanvasInteractionMode.Pressing when Travelled(screenX, screenY) < DragThreshold:
                return null;

            case CanvasInteractionMode.Pressing:
            case CanvasInteractionMode.DraggingNode:
                Mode = CanvasInteractionMode.DraggingNode;
                _nodePosition = FromPress(screenX, screenY);
                return new NodeMove(PressedScene.GetValueOrDefault(), _nodePosition);

            case CanvasInteractionMode.DrawingEdge:
                DraftEnd = FromPress(screenX, screenY);
                DropTarget = TargetUnder(DraftEnd.Value);
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Ends the gesture. Returns the dragged node's final position, or the link drawn and the scene
    /// it was dropped on; <see langword="null"/> for a click, a pan, or a draft dropped anywhere
    /// but another scene.
    /// </summary>
    public CanvasRelease? PointerUp()
    {
        CanvasRelease? result = Mode switch
        {
            CanvasInteractionMode.DraggingNode => new NodeMove(PressedScene.GetValueOrDefault(), _nodePosition),

            // The map can change between the last move and the release; the drop is tested again.
            CanvasInteractionMode.DrawingEdge when DraftEnd is { } end && TargetUnder(end) is { } target =>
                new EdgeDrawResult(PressedScene.GetValueOrDefault(), target),

            _ => null,
        };

        _clickAfterDrag = result is NodeMove || (Mode == CanvasInteractionMode.Panning && _panTravelled);
        End();
        return result;
    }

    /// <summary>
    /// Ends the gesture with nothing to report: no position to save, no link to make, and no click
    /// to claim. For a pressed scene that has left the story mid-gesture, an abandoned draft, or a
    /// cancelled pointer.
    /// </summary>
    public void Cancel()
    {
        _clickAfterDrag = false;
        End();
    }

    /// <summary>
    /// Reports whether the click the browser fires after the most recent release belongs to a
    /// drag or a pan, and forgets it, so it is answered exactly once: that click must not select.
    /// </summary>
    public bool ClaimClickAfterDrag()
    {
        var claimed = _clickAfterDrag;
        _clickAfterDrag = false;
        return claimed;
    }

    private bool Begin(SceneId? scene, double screenX, double screenY)
    {
        if (Mode != CanvasInteractionMode.Idle)
        {
            return false;
        }

        // A touch drag fires no click, so the mark must not outlive the next press.
        _clickAfterDrag = false;
        _panTravelled = false;
        PressedScene = scene;
        _pressX = _lastX = screenX;
        _pressY = _lastY = screenY;
        return true;
    }

    private void End()
    {
        Mode = CanvasInteractionMode.Idle;
        PressedScene = null;
        DraftEnd = null;
        DropTarget = null;
        _hitTest = null;
    }

    // Where the pressed point has travelled, in canvas units, from the node or port it started on.
    private ScenePosition FromPress(double screenX, double screenY) =>
        new(
            _nodeOrigin.X + ((screenX - _pressX) / _viewport.Scale),
            _nodeOrigin.Y + ((screenY - _pressY) / _viewport.Scale));

    private SceneId? TargetUnder(ScenePosition end) =>
        _hitTest?.Invoke(end) is { } scene && scene != PressedScene ? scene : null;

    private double Travelled(double screenX, double screenY) =>
        Math.Sqrt(Math.Pow(screenX - _pressX, 2) + Math.Pow(screenY - _pressY, 2));
}

/// <summary>
/// What a released gesture leaves for the canvas to do.
/// </summary>
public abstract record CanvasRelease;

/// <summary>
/// A node's new position, in canvas units, as a drag moves it or leaves it.
/// </summary>
/// <param name="Scene">The scene whose node moved.</param>
/// <param name="Position">Where the node's top-left corner now sits.</param>
public sealed record NodeMove(SceneId Scene, ScenePosition Position) : CanvasRelease;

/// <summary>
/// A link drawn on the canvas from one scene's port and dropped on another scene.
/// </summary>
/// <param name="Source">The scene the link leaves.</param>
/// <param name="Target">The scene it was dropped on.</param>
public sealed record EdgeDrawResult(SceneId Source, SceneId Target) : CanvasRelease;
