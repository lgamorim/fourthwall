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

    private static readonly IReadOnlyDictionary<SceneId, ScenePosition> NoPositions =
        new Dictionary<SceneId, ScenePosition>();

    private static readonly string ThumbnailX = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailX);
    private static readonly string ThumbnailY = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailY);
    private static readonly string ThumbnailSize = CanvasGeometry.Invariant(CanvasGeometry.ThumbnailSize);
    private static readonly string CornerRadius = CanvasGeometry.Invariant(CanvasGeometry.CornerRadius);

    private readonly CancellationTokenSource _disposal = new();
    private readonly CanvasViewport _viewport = new();
    private readonly CanvasInteraction _interaction;

    // The story whose positions were last asked for, and the story _saved holds positions for;
    // they differ only while a load is in flight. _framedStory is the one the viewport was framed
    // for on open, so a later resize never moves the map under the creator.
    private Story? _requestedStory;
    private Story? _positionsStory;
    private Story? _framedStory;
    private IReadOnlyDictionary<SceneId, ScenePosition> _saved = NoPositions;
    private CanvasModel? _model;
    private string? _loadError;
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

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    private string? HostStateClass => _interaction.Mode == CanvasInteractionMode.Panning ? "canvas-panning" : null;

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

        _interaction.PointerMove(clientX, clientY);
        StateHasChanged();
    });

    /// <summary>
    /// Releases the captured pointer at its final window position.
    /// </summary>
    [JSInvokable]
    public Task UpAsync(double clientX, double clientY) => InvokeAsync(() =>
    {
        if (_disposal.IsCancellationRequested)
        {
            return;
        }

        _interaction.PointerMove(clientX, clientY);
        _interaction.PointerUp();
        StateHasChanged();
    });

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

    protected override void OnInitialized() => _persisting = State.RegisterOnPersisting(PersistLayoutAsync);

    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(Story, _requestedStory))
        {
            // Drop the previous story's map first, so a slow load never shows it under the new title.
            var story = Story;
            _requestedStory = story;
            _model = null;
            _viewport.Reset();

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
        FrameIfNeeded();
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

    private void OnGroundPointerDown(PointerEventArgs args)
    {
        // Only the primary button slides the map; the shim captures the pointer for the same button.
        if (args.Button != 0)
        {
            return;
        }

        _interaction.PointerDown(scene: null, nodePosition: null, args.ClientX, args.ClientY);
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
                    scene => _saved.TryGetValue(scene.Id, out var position) ? position : (ScenePosition?)null));
        }

        return Task.CompletedTask;
    }

    private Task SelectAsync(SceneId sceneId) => SelectedSceneIdChanged.InvokeAsync(sceneId);
}
