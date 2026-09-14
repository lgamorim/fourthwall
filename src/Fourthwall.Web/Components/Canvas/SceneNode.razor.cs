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

    [Parameter]
    public EventCallback OnSelected { get; set; }

    /// <summary>
    /// Raised when a pointer presses the node, with the press's window coordinates, so the canvas
    /// can begin a drag. The press stops here: the ground under the node is not held.
    /// </summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPointerDown { get; set; }

    /// <summary>
    /// Raised when the node receives focus, so the canvas can bring a scene the keyboard landed on
    /// into view.
    /// </summary>
    [Parameter]
    public EventCallback OnFocused { get; set; }

    private string Label => Scenes.Label(
        Node.Scene, Node.Scene.ImagePath is null ? LabelLength : LabelLengthBesideThumbnail);

    private string AccessibleName => $"{Scenes.Label(Node.Scene)}, {Node.Scene.Kind}";

    private string StateClasses =>
        $"{(IsSelected ? "node-selected" : null)} {(IsDragging ? "node-dragging" : null)}".Trim();

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
