using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Pages;

public partial class EditorDock
{
    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public Story Story { get; set; } = default!;

    [Parameter]
    public SceneId? SelectedSceneId { get; set; }

    /// <summary>
    /// Raised when the navigator or a validation chip picks a scene. The page owns the selection,
    /// so the canvas and the dock agree on it.
    /// </summary>
    [Parameter]
    public EventCallback<SceneId?> OnSceneSelected { get; set; }

    /// <summary>
    /// Raised after a mutation the story should be saved for. The page owns saving.
    /// </summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }
}
