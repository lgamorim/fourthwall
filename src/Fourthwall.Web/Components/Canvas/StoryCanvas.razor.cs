using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Fourthwall.Web.Components.Canvas;

public partial class StoryCanvas : IAsyncDisposable
{
    /// <summary>
    /// The colocated shim, relative to the web root. It is imported through <c>Assets</c> so the
    /// fingerprinted URL is requested.
    /// </summary>
    public const string ModulePath = "Components/Canvas/StoryCanvas.razor.js";

    // Prerendering runs the canvas twice per page load — once on the server, once when the circuit
    // attaches — and positions come from the story database. Persisting them across the handoff
    // means one read per load instead of two, without giving up prerendering.
    private const string LayoutStateKey = "story-canvas-layout";

    // How far one arrow key slides the map, and how much wheel travel makes one zoom step: a mouse
    // notch is 100 pixels in Chromium, and the shim normalises other delta modes to pixels.
    private const double KeyboardPanStep = 40;
    private const double WheelNotch = 100;

    // The domain accepts no blank choice label, so a choice drawn on the map is labelled with what
    // the label wants from the creator (design note §12.4); its scene is selected to rename it.
    private const string DefaultChoiceLabel = "Name this choice";

    private static readonly string ThumbnailX = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailX);
    private static readonly string ThumbnailY = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailY);
    private static readonly string ThumbnailSize = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailSize);
    private static readonly string CornerRadius = CanvasGeometry.Invariant(CanvasGeometry.CornerRadius);

    private readonly CancellationTokenSource _disposal = new();
    private readonly CanvasViewport _viewport = new();
    private readonly CanvasInteraction _interaction;

    // The story whose positions were last asked for, and the story _positions holds positions for;
    // they differ only while a load is in flight. _framedStory is the one the viewport was framed
    // for on open, so a later resize never moves the map under the creator.
    private Story? _requestedStory;
    private Story? _positionsStory;
    private Story? _framedStory;

    // Positions the store returned or the creator chose by dragging, by scene: what a drag writes
    // and what the prerender hands over. Scenes absent here are placed afresh on every render.
    private Dictionary<SceneId, ScenePosition> _positions = [];

    // Every scene's position as the model was last built: _positions plus the placed ones. A drag
    // updates one entry and rebuilds the model without running the layout again.
    private Dictionary<SceneId, ScenePosition> _placed = [];
    private CanvasModel? _model;

    // The link the creator last clicked. Page selection stays a scene; the link keeps its ribbon
    // only while its scene is the selected one.
    private CanvasEdgeKey? _selectedEdge;
    private string? _loadError;
    private string? _saveError;
    private PersistingComponentStateSubscription _persisting;
    private ElementReference _host;
    private IJSObjectReference? _module;
    private DotNetObjectReference<StoryCanvas>? _self;

    public StoryCanvas() => _interaction = new CanvasInteraction(_viewport);

    // default!: required parameters and injected services are assigned by the framework before
    // any member of the component runs, so these are never observed null.
    [Parameter]
    [EditorRequired]
    public Story Story { get; set; } = default!;

    /// <summary>
    /// Where the open story's node positions are kept. Read once per story, and written one scene
    /// at a time when a drag ends.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public ISceneLayoutStore Layout { get; set; } = default!;

    [Parameter]
    public SceneId? SelectedSceneId { get; set; }

    [Parameter]
    public EventCallback<SceneId?> SelectedSceneIdChanged { get; set; }

    /// <summary>
    /// Raised after the canvas changes the story — a scene added, a link drawn — so the page saves
    /// it. Awaited before a new scene's position is saved, which needs the scene's saved row.
    /// </summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    /// <summary>
    /// Raised with the story once its positions have been read (or could not be, and its scenes
    /// were laid out afresh), so the page knows the map can take a new scene.
    /// </summary>
    [Parameter]
    public EventCallback<Story> OnReady { get; set; }

    [Inject]
    private IStoryGraphFactory GraphFactory { get; set; } = default!;

    [Inject]
    private PersistentComponentState State { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    // A failed save is the more recent news and takes the line; a failed read stays for the story's
    // life underneath it.
    private string? Error => _saveError ?? _loadError;

    private string? HostStateClass => _interaction.Mode switch
    {
        CanvasInteractionMode.Panning => "canvas-panning",
        CanvasInteractionMode.DraggingNode => "canvas-dragging",
        CanvasInteractionMode.DrawingEdge => "canvas-drawing",
        _ => null,
    };

    // The link being drawn: along the pointer over paper, and snapped to exactly the link it will
    // make over a scene it can link to (design note §12.2).
    private string? DraftPath
    {
        get
        {
            if (_interaction.Mode != CanvasInteractionMode.DrawingEdge
                || _interaction.PressedScene is not { } sourceId
                || _interaction.DraftEnd is not { } end
                || !_placed.TryGetValue(sourceId, out var from))
            {
                return null;
            }

            if (_interaction.DropTarget is { } targetId && _placed.TryGetValue(targetId, out var to))
            {
                // A new choice takes the next offset among its scene's choices to that target; a
                // follow-up replaces the one there is, so it is always the first.
                var parallelIndex = Story.FindScene(sourceId)?.Choices.Count(choice => choice.TargetSceneId == targetId) ?? 0;
                return CanvasGeometry.EdgePath(from, to, parallelIndex);
            }

            return CanvasGeometry.DraftPath(from, end);
        }
    }

    // The draft previews what it will become: a choice's line, or a follow-up's leader dots.
    private string DraftClass =>
        _interaction.PressedScene is { } sourceId && Story.FindScene(sourceId)?.Kind == SceneKind.Linear
            ? "canvas-ghost-edge ghost-follow-up"
            : "canvas-ghost-edge ghost-choice";

    public async ValueTask DisposeAsync()
    {
        _persisting.Dispose();
        _disposal.Cancel();
        _disposal.Dispose();

        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("detach", _host);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the browser's listeners went with it.
        }

        _self?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Records the window's size, as the browser measures it, and frames the story the first time
    /// both its positions and a size are known.
    /// </summary>
    [JSInvokable]
    public Task ResizeAsync(double width, double height) => InvokeAsync(() =>
    {
        if (_disposal.IsCancellationRequested)
        {
            return;
        }

        _viewport.SetSize(width, height);
        FrameIfNeeded();
        StateHasChanged();
    });

    /// <summary>
    /// Moves the captured pointer, in window coordinates: slides the map or moves the pressed node.
    /// </summary>
    [JSInvokable]
    public Task MoveAsync(double clientX, double clientY) => InvokeAsync(() =>
    {
        if (_disposal.IsCancellationRequested)
        {
            return;
        }

        if (_interaction.PointerMove(clientX, clientY) is { } move)
        {
            Place(move);
        }

        StateHasChanged();
    });

    /// <summary>
    /// Releases the captured pointer at its final window position. A drag ends by saving the moved
    /// scene's position, and nothing else, so the story and its validation report are untouched; a
    /// link dropped on a scene is made, and the story saved.
    /// </summary>
    [JSInvokable]
    public Task UpAsync(double clientX, double clientY) => InvokeAsync(() => ReleaseAsync(clientX, clientY));

    /// <summary>
    /// The browser took the captured pointer back. A link being drawn is abandoned; any other
    /// gesture ends where the pointer was last seen, as a release does.
    /// </summary>
    [JSInvokable]
    public Task CancelAsync(double clientX, double clientY) => InvokeAsync(async () =>
    {
        if (_interaction.Mode != CanvasInteractionMode.DrawingEdge)
        {
            await ReleaseAsync(clientX, clientY);
            return;
        }

        if (_disposal.IsCancellationRequested)
        {
            return;
        }

        _interaction.Cancel();
        StateHasChanged();
    });

    /// <summary>
    /// Adds an empty Linear scene centred in the window, saves the story and then the scene's place,
    /// and selects it: the toolbar's "Add scene".
    /// </summary>
    public Task AddSceneAsync() =>
        AddSceneAtAsync(_viewport.ToWorld(_viewport.Width / 2, _viewport.Height / 2));

    /// <summary>
    /// Zooms about a point of the window, by wheel travel in pixels: towards the creator zooms in.
    /// </summary>
    [JSInvokable]
    public Task ZoomAsync(double localX, double localY, double deltaY) => InvokeAsync(() =>
    {
        if (_disposal.IsCancellationRequested)
        {
            return;
        }

        _viewport.ZoomAt(localX, localY, -deltaY / WheelNotch);
        StateHasChanged();
    });

    // The two view methods are entry points for the page, not event handlers of this component:
    // the render they need comes from a StateHasChanged of their own. Today the page's re-render
    // would reach the canvas anyway, through its reference-typed parameters; that is the page's
    // business, and these methods must not depend on it.

    /// <summary>
    /// Frames every scene in the window, centred, never enlarging past actual size: the toolbar's
    /// "Show whole story". Nothing to show returns the map to actual size.
    /// </summary>
    public void FitToStory()
    {
        _viewport.Fit(_model?.Bounds ?? CanvasBounds.Empty, CanvasGeometry.ContentMargin);
        StateHasChanged();
    }

    /// <summary>
    /// Returns the map to actual size with the page's origin at the window's top-left corner: the
    /// toolbar's "Actual size".
    /// </summary>
    public void ResetView()
    {
        _viewport.Reset();
        StateHasChanged();
    }

    protected override void OnInitialized() => _persisting = State.RegisterOnPersisting(PersistLayoutAsync);

    protected override async Task OnParametersSetAsync()
    {
        // The selection moved off the clicked link's scene: that link is no longer what is shown.
        if (_selectedEdge is { } selectedEdge && selectedEdge.Source != SelectedSceneId)
        {
            _selectedEdge = null;
        }

        if (!ReferenceEquals(Story, _requestedStory))
        {
            // Drop the previous story's map first, so a slow load never shows it under the new title.
            var story = Story;
            _requestedStory = story;
            _model = null;
            _viewport.Reset();

            IReadOnlyDictionary<SceneId, ScenePosition> saved = new Dictionary<SceneId, ScenePosition>();
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
                    loadError = $"The scenes' places on the map couldn't be read, so they're laid out afresh. {exception.Message}";
                }
            }

            // Another story may have been shown while this one loaded; its positions win.
            if (!ReferenceEquals(story, _requestedStory))
            {
                return;
            }

            _positions = new Dictionary<SceneId, ScenePosition>(saved);
            _loadError = loadError;
            _saveError = null;
            _positionsStory = story;

            // The page re-renders on this callback, and after a truly asynchronous load that render
            // re-enters this method before the call below returns. That is expected: the story's
            // positions are already recorded above, so the nested call takes the loaded path, and
            // placing and framing the scenes twice changes nothing.
            await OnReady.InvokeAsync(story);
        }

        // Still loading this story's positions: placing its scenes now would flash a layout that
        // the saved one is about to replace.
        if (!ReferenceEquals(Story, _positionsStory))
        {
            return;
        }

        // The page re-renders the canvas after every edit, so scenes added, removed, or rewired
        // since the last render are placed here. Placed positions are never saved: only a creator's
        // drag or a scene added on the map records one.
        PlaceScenes();
        FrameIfNeeded();

        // The held scene, or the one a link is drawn from, can be deleted from another tab mid-
        // gesture; the release that follows has nothing left to place, save, or link.
        if (_interaction.PressedScene is { } pressed && Story.FindScene(pressed) is null)
        {
            _interaction.Cancel();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _self = DotNetObjectReference.Create(this);
        var module = await JS.InvokeAsync<IJSObjectReference>("import", "./" + Assets[ModulePath]);

        if (_disposal.IsCancellationRequested)
        {
            // The story closed while the module loaded; there is nothing left to attach to.
            await module.DisposeAsync();
            return;
        }

        _module = module;
        await module.InvokeVoidAsync("attach", _host, _self);
    }

    // A story opens at actual size when its whole map fits the window, and showing the whole story
    // otherwise (design note §11.1). Framed once per story, as soon as its positions and the
    // window's size are both known, whichever arrives last.
    private void FrameIfNeeded()
    {
        if (_model is null || !_viewport.IsMeasured || ReferenceEquals(_framedStory, _positionsStory))
        {
            return;
        }

        _framedStory = _positionsStory;
        if (!_viewport.Shows(_model.Bounds))
        {
            _viewport.Fit(_model.Bounds, CanvasGeometry.ContentMargin);
        }
    }

    // Every scene placed afresh around the positions the creator chose, and the model rebuilt from
    // them: after a load, after the page's edits, and after the canvas's own.
    private void PlaceScenes()
    {
        _placed = new Dictionary<SceneId, ScenePosition>(AutoLayout.Place(Story, GraphFactory.Create(Story), _positions));
        _model = CanvasModel.Build(Story, _placed);
    }

    // The moved node is redrawn where the pointer has it, with its links, on every frame.
    private void Place(NodeMove move)
    {
        _placed[move.Scene] = move.Position;
        _model = CanvasModel.Build(Story, _placed);
    }

    private async Task ReleaseAsync(double clientX, double clientY)
    {
        if (_disposal.IsCancellationRequested)
        {
            return;
        }

        if (_interaction.PointerMove(clientX, clientY) is { } move)
        {
            Place(move);
        }

        var released = _interaction.PointerUp();
        StateHasChanged();

        switch (released)
        {
            case NodeMove dropped:
                await SavePositionAsync(dropped.Scene, dropped.Position);
                break;
            case EdgeDrawResult drawn:
                await LinkAsync(drawn);
                break;
            default:
                break;
        }
    }

    // The order is the foreign key's: the story is saved first, because a position names a scene
    // row that must exist. If that save failed, the store rejects the position and says so on the
    // error line; the page stays where it was placed for the session either way.
    private async Task AddSceneAtAsync(ScenePosition centre)
    {
        // Still reading this story's positions: there is no map to place a scene on yet.
        if (!ReferenceEquals(Story, _positionsStory))
        {
            return;
        }

        var position = new ScenePosition(
            centre.X - (CanvasGeometry.NodeWidth / 2), centre.Y - (CanvasGeometry.NodeHeight / 2));
        var scene = Story.AddScene(SceneKind.Linear, string.Empty);
        _positions[scene.Id] = position;
        PlaceScenes();

        await OnChanged.InvokeAsync();
        if (_disposal.IsCancellationRequested)
        {
            return;
        }

        // The page reports its own save failures and the callback carries no result, so this save
        // runs even after a failed story save. What keeps it from recording a position for an
        // unsaved scene is the database: the connection enables foreign keys (Foreign Keys=True in
        // SqliteConnectionFactory), editor_scene_layout.scene_id references scenes(id), and the
        // store reports the violation as "The scene isn't in the saved story yet."
        await SavePositionAsync(scene.Id, position);
        if (!_disposal.IsCancellationRequested)
        {
            await SelectAsync(scene.Id, edge: null);
        }
    }

    // A Choice gains a choice to the target; a Linear scene flows into it, replacing the follow-up
    // it had, as the inspector's dropdown does. The source is checked again: another tab can delete
    // it or make it an Ending while the link is drawn, and nothing leaves an Ending.
    private async Task LinkAsync(EdgeDrawResult drawn)
    {
        if (Story.FindScene(drawn.Source) is not { } source || Story.FindScene(drawn.Target) is null)
        {
            return;
        }

        switch (source.Kind)
        {
            case SceneKind.Choice:
                Story.WireChoice(source.Id, DefaultChoiceLabel, drawn.Target);
                break;
            case SceneKind.Linear:
                Story.SetFollowUp(source.Id, drawn.Target);
                break;
            case SceneKind.Ending:
                return;
            default:
                throw new InvalidOperationException($"Unknown scene kind '{source.Kind}'.");
        }

        PlaceScenes();
        await OnChanged.InvokeAsync();

        // The story can close while the page saves; as in AddSceneAtAsync, nobody is left to select for.
        if (!_disposal.IsCancellationRequested)
        {
            await SelectAsync(source.Id, edge: null);
        }
    }

    private async Task SavePositionAsync(SceneId sceneId, ScenePosition position)
    {
        // The page stays where it was dropped for the session either way; the store decides whether
        // it is still there when the story reopens.
        _positions[sceneId] = position;
        _saveError = null;

        try
        {
            await Layout.SaveAsync(
                new Dictionary<SceneId, ScenePosition> { [sceneId] = position }, _disposal.Token);
        }
        catch (OperationCanceledException) when (_disposal.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (UserFacingFailures.Includes(exception))
        {
            _saveError = $"That scene's place on the map couldn't be saved. {exception.Message}";
        }

        StateHasChanged();
    }

    private void OnGroundPointerDown(PointerEventArgs args)
    {
        // Only the primary button slides the map; the shim captures the pointer for the same button.
        if (args.Button != 0)
        {
            return;
        }

        _interaction.PointerDown(scene: null, nodePosition: null, args.ClientX, args.ClientY);
    }

    private void OnNodePointerDown(SceneId sceneId, PointerEventArgs args)
    {
        if (args.Button != 0)
        {
            return;
        }

        _interaction.PointerDown(sceneId, _placed[sceneId], args.ClientX, args.ClientY);
    }

    // The draft starts at the port's centre and follows the pointer's travel, so no window-to-map
    // origin is needed from the browser; the scenes under its end are found against the map as it
    // is drawn at that moment.
    private void OnPortPointerDown(SceneId sceneId, PointerEventArgs args)
    {
        if (args.Button != 0)
        {
            return;
        }

        _interaction.PortDown(
            sceneId, CanvasGeometry.PortCentre(_placed[sceneId]), args.ClientX, args.ClientY,
            world => _model?.HitTest(world));
    }

    // Nodes and links stop their own double-clicks, so this one is on the paper and its offset is
    // measured from the svg, which fills the sheet the viewport reasons with.
    private Task OnGroundDoubleClickAsync(MouseEventArgs args) =>
        AddSceneAtAsync(_viewport.ToWorld(args.OffsetX, args.OffsetY));

    // Tab reaches every scene in navigator order, wherever the map was left; a scene that takes
    // focus wholly outside the window is centred so its ring can be seen (design note §11.5). One
    // still partly in view is left alone: a click focuses too, and must never move the map under
    // the pointer.
    private void OnNodeFocused(SceneId sceneId)
    {
        var position = _placed[sceneId];
        var bounds = new CanvasBounds(
            position.X, position.Y, position.X + CanvasGeometry.NodeWidth, position.Y + CanvasGeometry.NodeHeight);

        if (!_viewport.IsMeasured || _viewport.Touches(bounds))
        {
            return;
        }

        _viewport.CentreOn(new ScenePosition(
            position.X + (CanvasGeometry.NodeWidth / 2), position.Y + (CanvasGeometry.NodeHeight / 2)));
    }

    // The arrow names where the creator wants to look, so the map slides the other way. Keys with
    // a modifier are the browser's (Ctrl+- is its own zoom) and are left alone.
    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.CtrlKey || args.AltKey || args.MetaKey)
        {
            return;
        }

        switch (args.Key)
        {
            case "ArrowRight":
                _viewport.PanBy(-KeyboardPanStep, 0);
                break;
            case "ArrowLeft":
                _viewport.PanBy(KeyboardPanStep, 0);
                break;
            case "ArrowDown":
                _viewport.PanBy(0, -KeyboardPanStep);
                break;
            case "ArrowUp":
                _viewport.PanBy(0, KeyboardPanStep);
                break;
            case "+" or "=":
                _viewport.ZoomBy(1);
                break;
            case "-":
                _viewport.ZoomBy(-1);
                break;
            case "Escape" when _interaction.Mode == CanvasInteractionMode.DrawingEdge:
                // The release still arrives from the shim and finds nothing to end.
                _interaction.Cancel();
                break;
            default:
                break;
        }
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
                    scene => _positions.TryGetValue(scene.Id, out var position) ? position : (ScenePosition?)null));
        }

        return Task.CompletedTask;
    }

    // The browser fires a click for every press, the one that ended a drag or a pan included; a
    // drag moves and a click selects, never both.
    private Task ClickSceneAsync(SceneId sceneId) =>
        _interaction.ClaimClickAfterDrag() ? Task.CompletedTask : SelectAsync(sceneId, edge: null);

    private Task ClickEdgeAsync(CanvasEdgeKey edge) =>
        _interaction.ClaimClickAfterDrag() ? Task.CompletedTask : SelectAsync(edge.Source, edge);

    private bool IsSelectedEdge(CanvasEdge edge) => edge.Key == _selectedEdge && edge.Source == SelectedSceneId;

    private Task SelectAsync(SceneId sceneId, CanvasEdgeKey? edge)
    {
        _selectedEdge = edge;
        return SelectedSceneIdChanged.InvokeAsync(sceneId);
    }
}
