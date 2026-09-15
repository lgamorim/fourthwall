using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Canvas;

public partial class SceneEdge
{
    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public CanvasEdge Edge { get; set; } = default!;

    /// <summary>
    /// Whether this is the link the creator last clicked, while its scene is still selected.
    /// </summary>
    [Parameter]
    public bool IsSelected { get; set; }

    /// <summary>
    /// Raised when the line is clicked. The canvas decides whether the click selects: the browser
    /// clicks a link after a pan that started on it, too.
    /// </summary>
    [Parameter]
    public EventCallback OnSelected { get; set; }

    // A follow-up is the key without a choice index, whatever its label says.
    private bool IsChoice => Edge.Key.ChoiceIndex is not null;

    private string GroupClass =>
        $"canvas-edge {(IsChoice ? "edge-choice" : "edge-follow-up")}{(IsSelected ? " edge-selected" : null)}";
}
