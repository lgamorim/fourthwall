using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Canvas;

public partial class SceneEdge
{
    // The label sits in the gutter between columns; longer ones are cut, whole in the <title>.
    private const int LabelLength = 16;

    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public CanvasEdge Edge { get; set; } = default!;

    // A follow-up is the key without a choice index, whatever its label says.
    private bool IsChoice => Edge.Key.ChoiceIndex is not null;
}
