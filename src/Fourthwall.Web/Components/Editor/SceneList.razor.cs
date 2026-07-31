using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Editor;

public partial class SceneList
{
    private const int SnippetLength = 60;

    private SceneKind _createKind = SceneKind.Linear;
    private OutcomeKind _createOutcome = OutcomeKind.Death;
    private string _createOutcomeLabel = string.Empty;
    private string _createText = string.Empty;
    private string? _createError;
    private SceneId? _pendingDelete;

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

    private static string Snippet(Scene scene)
    {
        // Scenes carry narrative text and no title, so the list labels a row with the text itself —
        // and empty text is legal while authoring, which still needs something to click.
        if (string.IsNullOrWhiteSpace(scene.Text))
        {
            return "(no text)";
        }

        var text = scene.Text.Trim();
        return text.Length <= SnippetLength ? text : string.Concat(text.AsSpan(0, SnippetLength), "…");
    }

    // Story.Scenes comes from a dictionary and has no inherent order, so the list imposes one: the
    // start scene first, then alphabetically, with the identifier breaking ties so identical text
    // cannot make rows swap places between renders.
    private IEnumerable<Scene> OrderedScenes() =>
        Story.Scenes
            .OrderByDescending(scene => scene.Id == Story.StartSceneId)
            .ThenBy(scene => scene.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(scene => scene.Id.Value);

    private Task SelectAsync(SceneId sceneId) => SelectedSceneIdChanged.InvokeAsync(sceneId);

    private void AskToDelete(SceneId sceneId) => _pendingDelete = sceneId;

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
        if (_createKind == SceneKind.Ending)
        {
            if (Outcomes.RequiresLabel(_createOutcome) && string.IsNullOrWhiteSpace(_createOutcomeLabel))
            {
                _createError = "An outcome of Other needs a label.";
                return;
            }

            outcome = Outcomes.Build(_createOutcome, _createOutcomeLabel);
        }

        var scene = Story.AddScene(_createKind, _createText, outcome);
        _createText = string.Empty;
        _createOutcomeLabel = string.Empty;

        await SelectedSceneIdChanged.InvokeAsync(scene.Id);
        await OnChanged.InvokeAsync();
    }
}
