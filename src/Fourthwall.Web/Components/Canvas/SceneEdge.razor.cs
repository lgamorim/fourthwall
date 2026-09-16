using Fourthwall.Application;

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
    /// The severity of the report's problems with the scene this link leaves, if any. Violations
    /// name scenes, not links, so a link is blamed for where it starts.
    /// </summary>
    [Parameter]
    public ValidationSeverity? Severity { get; set; }

    /// <summary>
    /// Raised when the line is clicked. The canvas decides whether the click selects: the browser
    /// clicks a link after a pan that started on it, too.
    /// </summary>
    [Parameter]
    public EventCallback OnSelected { get; set; }

    // A follow-up is the key without a choice index, whatever its label says.
    private bool IsChoice => Edge.Key.ChoiceIndex is not null;

    private string GroupClass => string.Join(
        ' ',
        new[]
        {
            "canvas-edge",
            IsChoice ? "edge-choice" : "edge-follow-up",
            Severity switch
            {
                ValidationSeverity.Error => "edge-error",
                ValidationSeverity.Warning => "edge-warning",
                _ => null,
            },
            IsSelected ? "edge-selected" : null,
        }.OfType<string>());

    // Selection takes the line and its arrowhead; the link's mark keeps the severity meanwhile.
    private string Marker => (IsSelected, Severity) switch
    {
        (true, _) => "url(#canvas-arrow-selected)",
        (_, ValidationSeverity.Error) => "url(#canvas-arrow-error)",
        (_, ValidationSeverity.Warning) => "url(#canvas-arrow-warning)",
        _ => "url(#canvas-arrow)",
    };
}
