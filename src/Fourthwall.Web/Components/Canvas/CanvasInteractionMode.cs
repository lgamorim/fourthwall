namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// What the pointer is doing to the canvas.
/// </summary>
public enum CanvasInteractionMode
{
    /// <summary>
    /// No button is held.
    /// </summary>
    Idle,

    /// <summary>
    /// A node is pressed but has not yet travelled far enough to count as a drag; releasing now is
    /// a click.
    /// </summary>
    Pressing,

    /// <summary>
    /// The ground is held and the map slides with the pointer.
    /// </summary>
    Panning,

    /// <summary>
    /// A node is held past the drag threshold and moves with the pointer.
    /// </summary>
    DraggingNode,

    /// <summary>
    /// A node's port is held and a draft link follows the pointer; releasing over another scene
    /// links to it.
    /// </summary>
    DrawingEdge,
}
