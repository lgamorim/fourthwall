using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Canvas;

public partial class StoryCanvas
{
    private static readonly IReadOnlyDictionary<SceneId, ScenePosition> NoPositions =
        new Dictionary<SceneId, ScenePosition>();

    private static readonly string ThumbnailX = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailX);
    private static readonly string ThumbnailY = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailY);
    private static readonly string ThumbnailSize = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailSize);

    // The story whose positions were last asked for, and the story _saved holds positions for;
    // they differ only while a load is in flight.
    private Story? _requestedStory;
    private Story? _positionsStory;
    private IReadOnlyDictionary<SceneId, ScenePosition> _saved = NoPositions;
    private CanvasModel? _model;
    private string? _loadError;

    // default!: required parameters and injected services are assigned by the framework before
    // any member of the component runs, so these are never observed null.
    [Parameter]
    [EditorRequired]
    public Story Story { get; set; } = default!;

    /// <summary>
    /// Where the open story's node positions are kept. Read once per story; this component never
    /// writes it until M21 persists a drag.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public ISceneLayoutStore Layout { get; set; } = default!;

    [Parameter]
    public SceneId? SelectedSceneId { get; set; }

    [Parameter]
    public EventCallback<SceneId?> SelectedSceneIdChanged { get; set; }

    [Inject]
    private IStoryGraphFactory GraphFactory { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(Story, _requestedStory))
        {
            // Drop the previous story's map first, so a slow load never shows it under the new title.
            var story = Story;
            _requestedStory = story;
            _model = null;

            var saved = NoPositions;
            string? loadError = null;
            try
            {
                saved = await Layout.LoadAsync();
            }
            catch (Exception exception) when (UserFacingFailures.Includes(exception))
            {
                // Every scene is still placed; the map just may not look the way it was left.
                loadError = exception.Message;
            }

            // Another story may have been shown while this one loaded; its positions win.
            if (!ReferenceEquals(story, _requestedStory))
            {
                return;
            }

            _saved = saved;
            _loadError = loadError;
            _positionsStory = story;
        }

        // Still loading this story's positions: placing its scenes now would flash a layout that
        // the saved one is about to replace.
        if (!ReferenceEquals(Story, _positionsStory))
        {
            return;
        }

        // The page re-renders the canvas after every edit, so scenes added, removed, or rewired
        // since the last render are placed here. Placed positions are never saved: only a creator's
        // drag records one.
        var positions = AutoLayout.Place(Story, GraphFactory.Create(Story), _saved);
        _model = CanvasModel.Build(Story, positions);
    }

    private Task SelectAsync(SceneId sceneId) => SelectedSceneIdChanged.InvokeAsync(sceneId);
}
