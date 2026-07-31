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

    private static string Snippet(Scene scene)
    {
        // Scenes carry narrative text and no title, so the list labels a row with the text itself —
        // and empty text is legal while authoring, which still needs something to click.
        if (string.IsNullOrWhiteSpace(scene.Text))
        {
            return "(no text)";
        }

        var text = scene.Text.Trim();
        if (text.Length <= SnippetLength)
        {
            return text;
        }

        // Back off a character when the cut would land inside a surrogate pair, so the snippet
        // never ends in half an emoji.
        var length = char.IsHighSurrogate(text[SnippetLength - 1]) ? SnippetLength - 1 : SnippetLength;
        return string.Concat(text.AsSpan(0, length), "…");
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
