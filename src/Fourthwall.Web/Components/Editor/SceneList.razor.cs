using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Editor;

public partial class SceneList
{
    private SceneKind _createKind = SceneKind.Linear;
    private OutcomeKind _createOutcome = OutcomeKind.Death;
    private string _createOutcomeLabel = string.Empty;
    private string _createText = string.Empty;
    private string? _createError;
    private SceneId? _pendingDelete;

    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public Story Story { get; set; } = default!;

    [Parameter]
    public SceneId? SelectedSceneId { get; set; }

    [Parameter]
    public EventCallback<SceneId?> SelectedSceneIdChanged { get; set; }

    /// <summary>
    /// Raised after a mutation the story should be saved for. The page owns saving, so persistence
    /// stays in one place rather than spreading across every editing component.
    /// </summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    // Deleting a scene also strips every transition pointing at it, which is invisible from the
    // row being deleted. Say so — but only when there is something to lose, the same rule the
    // kind-change prompt follows.
    private string DeleteConfirmation(SceneId sceneId)
    {
        var inbound = Story.Scenes.Sum(scene => scene.OutgoingSceneIds.Count(id => id == sceneId));

        return inbound == 0
            ? "Confirm delete"
            : $"Confirm delete and remove {inbound} link{(inbound == 1 ? string.Empty : "s")} to it";
    }

    private Task SelectAsync(SceneId sceneId) => SelectedSceneIdChanged.InvokeAsync(sceneId);

    private void AskToDelete(SceneId sceneId) => _pendingDelete = sceneId;

    private void CancelDelete() => _pendingDelete = null;

    private async Task SetStartAsync(SceneId sceneId)
    {
        Story.SetStartScene(sceneId);
        await OnChanged.InvokeAsync();
    }

    private async Task DeleteAsync(SceneId sceneId)
    {
        _pendingDelete = null;
        if (!Story.RemoveScene(sceneId))
        {
            return;
        }

        if (SelectedSceneId == sceneId)
        {
            await SelectedSceneIdChanged.InvokeAsync(null);
        }

        await OnChanged.InvokeAsync();
    }

    private async Task CreateAsync()
    {
        _createError = null;

        EndingOutcome? outcome = null;
        if (_createKind == SceneKind.Ending
            && !Outcomes.TryBuild(_createOutcome, _createOutcomeLabel, out outcome, out _createError))
        {
            return;
        }

        var scene = Story.AddScene(_createKind, _createText, outcome);
        _createText = string.Empty;
        _createOutcomeLabel = string.Empty;

        await SelectedSceneIdChanged.InvokeAsync(scene.Id);
        await OnChanged.InvokeAsync();
    }
}
