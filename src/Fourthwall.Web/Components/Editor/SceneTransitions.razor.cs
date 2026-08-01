using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Editor;

public partial class SceneTransitions
{
    private SceneId _shownScene;
    private string _addLabel = string.Empty;
    private string _addTarget = string.Empty;
    private string? _addError;
    private string? _error;
    private int _revision;

    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so these are never observed null.
    [Parameter]
    [EditorRequired]
    public Story Story { get; set; } = default!;

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
        if (_shownScene != Scene.Id)
        {
            // A different scene is being edited: a half-typed choice and a stale error belong to
            // the scene they were typed against, not this one.
            _shownScene = Scene.Id;
            _addLabel = string.Empty;
            _addError = null;
            _error = null;
        }

        // Re-checked on every render, not just the first: the scene this form points at can be
        // deleted while the form is showing, and the select would still hold its identifier —
        // wiring a choice to a scene the story no longer has.
        if (!TryParse(_addTarget, out var target) || Story.FindScene(target) is null)
        {
            _addTarget = Scenes.Ordered(Story).FirstOrDefault()?.Id.Value.ToString() ?? string.Empty;
        }
    }

    private async Task AddAsync()
    {
        _addError = null;

        if (string.IsNullOrWhiteSpace(_addLabel))
        {
            _addError = "A choice needs text the reader can read.";
            return;
        }

        if (!TryParse(_addTarget, out var target))
        {
            _addError = "Choose the scene this choice leads to.";
            return;
        }

        Story.WireChoice(Scene.Id, _addLabel, target);
        _addLabel = string.Empty;
        await OnChanged.InvokeAsync();
    }

    private async Task RelabelAsync(int index, string? label)
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(label))
        {
            // Story.RelabelChoice rejects a blank label; say so rather than let it throw. The
            // rendered value is unchanged, so Blazor's diff would leave the rejected text in the
            // field — bump the revision the row is keyed by to force it back to the real label.
            _error = "A choice needs text the reader can read.";
            _revision++;
            return;
        }

        Story.RelabelChoice(Scene.Id, index, label);
        await OnChanged.InvokeAsync();
    }

    private async Task RetargetAsync(int index, string? targetSceneId)
    {
        _error = null;

        if (!TryParse(targetSceneId, out var target))
        {
            return;
        }

        Story.RetargetChoice(Scene.Id, index, target);
        await OnChanged.InvokeAsync();
    }

    private async Task MoveAsync(int fromIndex, int toIndex)
    {
        Story.MoveChoice(Scene.Id, fromIndex, toIndex);
        await OnChanged.InvokeAsync();
    }

    private async Task RemoveAsync(int index)
    {
        // No confirmation: a choice is a label and a target, retyped in seconds. Deleting a scene
        // asks because it takes its text and every transition pointing at it.
        Story.RemoveChoice(Scene.Id, index);
        await OnChanged.InvokeAsync();
    }

    private async Task SetFollowUpAsync(string? targetSceneId)
    {
        if (TryParse(targetSceneId, out var target))
        {
            Story.SetFollowUp(Scene.Id, target);
        }
        else
        {
            Story.ClearFollowUp(Scene.Id);
        }

        await OnChanged.InvokeAsync();
    }

    private static bool TryParse(string? sceneId, out SceneId parsed)
    {
        if (Guid.TryParse(sceneId, out var value) && value != Guid.Empty)
        {
            parsed = new SceneId(value);
            return true;
        }

        parsed = default;
        return false;
    }
}
