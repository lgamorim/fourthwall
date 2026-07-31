using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Editor;

public partial class SceneInspector
{
    private SceneId _shownScene;
    private SceneKind _kind;
    private OutcomeKind _outcome;
    private string _outcomeLabel = string.Empty;
    private string _text = string.Empty;
    private string? _error;
    private bool _pendingKindChange;

    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public Scene Scene { get; set; } = default!;

    /// <summary>
    /// Raised after a mutation the story should be saved for.
    /// </summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    protected override void OnParametersSet()
    {
        if (_shownScene == Scene.Id)
        {
            return;
        }

        // A different scene is being inspected: the form starts over rather than carrying the last
        // scene's half-finished edits across.
        _shownScene = Scene.Id;
        _kind = Scene.Kind;
        _outcome = Scene.Outcome?.Kind ?? OutcomeKind.Death;
        _outcomeLabel = Scene.Outcome?.Label ?? string.Empty;
        _text = Scene.Text;
        _error = null;
        _pendingKindChange = false;
    }

    private async Task OnTextChangedAsync()
    {
        Scene.SetText(_text);
        await OnChanged.InvokeAsync();
    }

    private async Task OnKindSelectedAsync()
    {
        _error = null;

        if (_kind == Scene.Kind)
        {
            _pendingKindChange = false;
            return;
        }

        // Only ask when there is something to lose: ChangeKind discards transitions the new kind
        // cannot carry, and a scene with none loses nothing.
        if (Scene.Choices.Count > 0 || Scene.FollowUpSceneId is not null)
        {
            _pendingKindChange = true;
            return;
        }

        await ApplyKindAsync();
    }

    private async Task ApplyKindAsync()
    {
        if (_kind == SceneKind.Ending)
        {
            if (!TryBuildOutcome(out var outcome))
            {
                return;
            }

            Scene.ChangeKind(SceneKind.Ending, outcome);
        }
        else
        {
            Scene.ChangeKind(_kind);
        }

        _pendingKindChange = false;
        await OnChanged.InvokeAsync();
    }

    private void CancelKindChange()
    {
        _kind = Scene.Kind;
        _pendingKindChange = false;
    }

    private async Task OnOutcomeChangedAsync()
    {
        _error = null;

        if (_pendingKindChange || Scene.Kind != SceneKind.Ending)
        {
            // The outcome is being chosen for a kind change that has not been applied yet.
            return;
        }

        if (!TryBuildOutcome(out var outcome))
        {
            return;
        }

        // The kind is unchanged, so this only replaces the outcome.
        Scene.ChangeKind(SceneKind.Ending, outcome);
        await OnChanged.InvokeAsync();
    }

    private bool TryBuildOutcome(out EndingOutcome? outcome) =>
        Outcomes.TryBuild(_outcome, _outcomeLabel, out outcome, out _error);
}
