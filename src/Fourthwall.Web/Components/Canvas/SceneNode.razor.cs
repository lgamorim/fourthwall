using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fourthwall.Web.Components.Canvas;

public partial class SceneNode
{
    // SVG text does not wrap: the label fits the room left of the right edge, less beside a
    // thumbnail. The node's <title> carries the whole text.
    private const int LabelLength = 22;
    private const int LabelLengthBesideThumbnail = 15;

    // Every number in the markup is pre-formatted, so no culture can write "12,5" into an attribute.
    // The text starts right of the ribbon's margin, where the navigator row's text starts too.
    private static readonly string TextX = CanvasGeometry.Invariant(20);
    private static readonly string LabelY = CanvasGeometry.Invariant(28);
    private static readonly string CaptionY = CanvasGeometry.Invariant(47);
    private static readonly string ThumbnailX = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailX);
    private static readonly string ThumbnailY = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailY);
    private static readonly string ThumbnailSize = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailSize);
    private static readonly string CornerRadius = CanvasGeometry.Invariant(CanvasGeometry.CornerRadius);
    private static readonly string StartTagY = CanvasGeometry.Invariant(-17);
    private static readonly string StartTagWidth = CanvasGeometry.Invariant(52);
    private static readonly string StartTagHeight = CanvasGeometry.Invariant(17);
    private static readonly string StartTagTextX = CanvasGeometry.Invariant(26);
    private static readonly string StartTagTextY = CanvasGeometry.Invariant(-5);
    private static readonly string PortX = CanvasGeometry.Invariant(CanvasGeometry.NodeWidth);
    private static readonly string PortY = CanvasGeometry.Invariant(CanvasGeometry.NodeHeight / 2);
    private static readonly string PortRadius = CanvasGeometry.Invariant(CanvasGeometry.PortRadius);
    private static readonly string PortHitRadius = CanvasGeometry.Invariant(CanvasGeometry.PortHitRadius);

    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public CanvasNode Node { get; set; } = default!;

    [Parameter]
    public bool IsSelected { get; set; }

    /// <summary>
    /// Whether the creator is moving this node; it keeps its lifted fill while held.
    /// </summary>
    [Parameter]
    public bool IsDragging { get; set; }

    /// <summary>
    /// Whether the scene under a link being drawn is this one: releasing now links to it.
    /// </summary>
    [Parameter]
    public bool IsDropTarget { get; set; }

    /// <summary>
    /// Whether a link is being drawn from this node's port.
    /// </summary>
    [Parameter]
    public bool IsDrawingSource { get; set; }

    /// <summary>
    /// Raised when Enter or Space selects the node, as a button answers them.
    /// </summary>
    [Parameter]
    public EventCallback OnSelected { get; set; }

    /// <summary>
    /// Raised when the node is clicked. The browser clicks a node after a drag of it as well, so the
    /// canvas decides whether a click selects; a key press never needs that decision.
    /// </summary>
    [Parameter]
    public EventCallback OnClicked { get; set; }

    /// <summary>
    /// Raised when a pointer presses the node, with the press's window coordinates, so the canvas
    /// can begin a drag. The press stops here: the ground under the node is not held.
    /// </summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPointerDown { get; set; }

    /// <summary>
    /// Raised when a pointer presses the node's port, with the press's window coordinates, so the
    /// canvas can begin drawing a link. The press stops at the port: the node is not moved.
    /// </summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPortPointerDown { get; set; }

    /// <summary>
    /// Raised when the node receives focus, so the canvas can bring a scene the keyboard landed on
    /// into view.
    /// </summary>
    [Parameter]
    public EventCallback OnFocused { get; set; }

    // An empty scene's label is a prompt to write it, not a name (design note §12.4); the navigator
    // and the target dropdowns, where a scene is picked, keep Scenes.Label's stand-in.
    private bool HasText => !string.IsNullOrWhiteSpace(Node.Scene.Text);

    private string Label => HasText
        ? Scenes.Label(Node.Scene, Node.Scene.ImagePath is null ? LabelLength : LabelLengthBesideThumbnail)
        : "Write this scene";

    private string AccessibleName => $"{Scenes.Label(Node.Scene)}, {Node.Scene.Kind}";

    private string StateClasses => string.Join(
        ' ',
        new[]
        {
            IsSelected ? "node-selected" : null,
            IsDragging ? "node-dragging" : null,
            IsDropTarget ? "node-drop-target" : null,
            IsDrawingSource ? "node-drawing-source" : null,
        }.OfType<string>());

    // The class picks nothing the outline has not already drawn; it lets the stylesheet and a
    // reader of the markup tell kinds apart. Exhaustive, like SceneList's: a kind the editor does
    // not know is a defect.
    private string KindClass => Node.Scene.Kind switch
    {
        SceneKind.Choice => "node-kind-choice",
        SceneKind.Linear => "node-kind-linear",
        SceneKind.Ending => "node-kind-ending",
        _ => throw new InvalidOperationException($"Unknown scene kind '{Node.Scene.Kind}'."),
    };

    // A node is a button to the keyboard, so it answers the keys a button does.
    private Task OnKeyDownAsync(KeyboardEventArgs args) =>
        args.Key is "Enter" or " " ? OnSelected.InvokeAsync() : Task.CompletedTask;
}
