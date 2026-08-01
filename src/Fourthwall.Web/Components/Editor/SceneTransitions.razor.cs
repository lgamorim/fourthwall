using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Editor;

public partial class SceneTransitions
{
    private string _addLabel = string.Empty;
    private string _addTarget = string.Empty;
    private string? _addError;
    private string? _error;

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
        // The add form defaults to the first scene a creator would pick, and never carries a
        // half-typed label across to another scene.
        var first = Scenes.Ordered(Story).FirstOrDefault();
        if (_addTarget.Length == 0 && first is not null)
        {
            _addTarget = first.Id.Value.ToString();
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
            // Story.RelabelChoice rejects a blank label; say so rather than let it throw.
            _error = "A choice needs text the reader can read.";
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
