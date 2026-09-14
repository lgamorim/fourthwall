using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Canvas;

public partial class StoryCanvas : IDisposable
{
    // Prerendering runs the canvas twice per page load — once on the server, once when the circuit
    // attaches — and positions come from the story database. Persisting them across the handoff
    // means one read per load instead of two, without giving up prerendering.
    private const string LayoutStateKey = "story-canvas-layout";

    private static readonly IReadOnlyDictionary<SceneId, ScenePosition> NoPositions =
        new Dictionary<SceneId, ScenePosition>();

    private static readonly string ThumbnailX = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailX);
    private static readonly string ThumbnailY = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailY);
    private static readonly string ThumbnailSize = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailSize);
    private static readonly string CornerRadius = CanvasGeometry.Invariant(CanvasGeometry.CornerRadius);

    private readonly CancellationTokenSource _disposal = new();

    // The story whose positions were last asked for, and the story _saved holds positions for;
    // they differ only while a load is in flight.
    private Story? _requestedStory;
    private Story? _positionsStory;
    private IReadOnlyDictionary<SceneId, ScenePosition> _saved = NoPositions;
    private CanvasModel? _model;
    private string? _loadError;
    private PersistingComponentStateSubscription _persisting;

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

    [Inject]
    private PersistentComponentState State { get; set; } = default!;

    public void Dispose()
    {
        _persisting.Dispose();
        _disposal.Cancel();
        _disposal.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => _persisting = State.RegisterOnPersisting(PersistLayoutAsync);

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
            if (TryTakeHandedOverLayout(story) is { } handedOver)
            {
                saved = handedOver;
            }
            else
            {
                try
                {
                    saved = await Layout.LoadAsync(_disposal.Token);
                }
                catch (OperationCanceledException) when (_disposal.IsCancellationRequested)
                {
                    // The canvas is gone; nobody is left to show these positions to.
                    return;
                }
                catch (Exception exception) when (UserFacingFailures.Includes(exception))
                {
                    // Every scene is still placed; the map just may not look the way it was left.
                    loadError = exception.Message;
                }
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

    // The handoff names every scene of the story it was read for, each with its saved position or
    // none. The workspace is shared across tabs, so another story may be open by the time this pass
    // runs; positions handed over for a different set of scenes are not this story's.
    private Dictionary<SceneId, ScenePosition>? TryTakeHandedOverLayout(Story story)
    {
        if (!State.TryTakeFromJson<Dictionary<Guid, ScenePosition?>>(LayoutStateKey, out var handedOver)
            || handedOver is null
            || !handedOver.Keys.ToHashSet().SetEquals(story.Scenes.Select(scene => scene.Id.Value)))
        {
            return null;
        }

        return handedOver
            .Where(entry => entry.Value is not null)
            .ToDictionary(entry => new SceneId(entry.Key), entry => entry.Value.GetValueOrDefault());
    }

    private Task PersistLayoutAsync()
    {
        // A failed read is not handed over, so the next pass tries again.
        if (_positionsStory is { } story && _loadError is null)
        {
            State.PersistAsJson(
                LayoutStateKey,
                story.Scenes.ToDictionary(
                    scene => scene.Id.Value,
                    scene => _saved.TryGetValue(scene.Id, out var position) ? position : (ScenePosition?)null));
        }

        return Task.CompletedTask;
    }

    private Task SelectAsync(SceneId sceneId) => SelectedSceneIdChanged.InvokeAsync(sceneId);
}
