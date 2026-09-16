using Fourthwall.Application;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Canvas;

public partial class SceneEdgeLabel
{
    // The label sits in the gutter between columns; longer ones are cut, whole in the <title>.
    private const int LabelLength = 16;

    // The mark sits in a paper disc centred on the curve, the same size as the page tab's mark.
    private static readonly string MarkDiscRadius = CanvasGeometry.Invariant(6.5);
    private static readonly string MarkSize = CanvasGeometry.Invariant(9);
    private static readonly string MarkOffset = CanvasGeometry.Invariant(-4.5);

    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public CanvasEdge Edge { get; set; } = default!;

    /// <summary>
    /// Whether this label's link is the selected one.
    /// </summary>
    [Parameter]
    public bool IsSelected { get; set; }

    /// <summary>
    /// The severity of the report's problems with the scene this link leaves, if any.
    /// </summary>
    [Parameter]
    public ValidationSeverity? Severity { get; set; }

    private string MarkTransform => $"translate({Edge.MarkX} {Edge.MarkY})";

    private static string MarkClass(ValidationSeverity severity) =>
        severity == ValidationSeverity.Error ? "edge-mark-error" : "edge-mark-warning";

    private static string MarkHref(ValidationSeverity severity) =>
        severity == ValidationSeverity.Error ? "#canvas-mark-error" : "#canvas-mark-warning";

    /// <summary>
    /// Raised when the label is clicked; the canvas decides whether the click selects its link.
    /// </summary>
    [Parameter]
    public EventCallback OnSelected { get; set; }
}
