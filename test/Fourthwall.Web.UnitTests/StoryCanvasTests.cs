using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Infrastructure;
using Fourthwall.Web.Components.Canvas;
using Fourthwall.Web.Composition;

using System.Text.Json;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fourthwall.Web.UnitTests;

public class StoryCanvasTests : BunitContext
{
    private const string LayoutStateKey = "story-canvas-layout";
    private const string ModulePath = "./Components/Canvas/StoryCanvas.razor.js";

    private readonly FakeSceneLayoutStore _layout = new();

    // The shim is an accepted untestable boundary: the module is a bUnit stand-in that records
    // what the canvas asks of it, and the canvas's [JSInvokable] methods are called directly.
    private readonly BunitJSModuleInterop _module;

    // The host registers PersistentComponentState as part of AddRazorComponents; bUnit does not.
    private readonly ComponentStatePersistenceManager _persistence =
        new(NullLogger<ComponentStatePersistenceManager>.Instance);

    public StoryCanvasTests()
    {
        Services.AddSingleton<IStoryGraphFactory>(new Graph1xStoryGraphFactory());
        Services.AddSingleton(_persistence.State);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
    }

    [Fact]
    public void Should_DrawANodePerScene_When_TheStoryHasScenes()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var fork = story.AddScene(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Equal(
            new string?[] { storm.Id.Value.ToString(), fork.Id.Value.ToString() }.Order(),
            cut.FindAll(".canvas-node").Select(node => node.GetAttribute("data-scene-id")).Order());
    }

    [Fact]
    public void Should_MarkEachNodesKind_When_Rendered()
    {
        // Arrange
        var story = new Story("The Wreck");
        var fork = story.AddScene(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Contains("node-kind-choice", NodeFor(cut, fork.Id).ClassList);
    }

    [Fact]
    public void Should_MarkTheStartScene_When_TheStoryHasOne()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var fork = story.AddScene(SceneKind.Choice, "A fork");
        story.SetStartScene(storm.Id);

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.NotNull(NodeFor(cut, storm.Id).QuerySelector(".node-start"));
        Assert.Null(NodeFor(cut, fork.Id).QuerySelector(".node-start"));
    }

    [Fact]
    public void Should_ShowAThumbnail_When_ASceneHasAnImage()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        storm.AttachImage("assets/storm.png");

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Equal(
            StoryAssetEndpoint.UrlPrefix + "assets/storm.png",
            cut.Find($".canvas-node[data-scene-id='{storm.Id.Value}'] .node-thumbnail image").GetAttribute("href"));
    }

    [Fact]
    public void Should_DrawALabelledLinkPerChoice_When_ASceneHasChoices()
    {
        // Arrange — two choices to the same scene are two links, not one.
        var story = new Story("The Wreck");
        var fork = story.AddScene(SceneKind.Choice, "A fork");
        var deck = story.AddScene(SceneKind.Linear, "Below deck");
        story.WireChoice(fork.Id, "Go below", deck.Id);
        story.WireChoice(fork.Id, "Climb down", deck.Id);

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Equal(
            ["Climb down", "Go below"],
            cut.FindAll(".canvas-edge-labels .edge-label-text").Select(label => label.TextContent).Order());
    }

    [Fact]
    public void Should_DrawAnUnlabelledLink_When_ASceneHasAFollowUp()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var deck = story.AddScene(SceneKind.Linear, "Below deck");
        story.SetFollowUp(storm.Id, deck.Id);

        // Act
        var cut = RenderCanvas(story);

        // Assert
        var link = Assert.Single(cut.FindAll(".canvas-edge"));
        Assert.Contains("edge-follow-up", link.ClassList);
        Assert.Empty(cut.FindAll(".edge-label"));
    }

    [Fact]
    public void Should_PaintLabelsOverLinksAndNodesOverBoth_When_Rendered()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var deck = story.AddScene(SceneKind.Linear, "Below deck");
        story.SetFollowUp(storm.Id, deck.Id);

        // Act
        var cut = RenderCanvas(story);

        // Assert — SVG paints in document order: every line first, so no line strikes through
        // another link's label, then the labels, then the nodes on top.
        Assert.Equal(
            ["canvas-edges", "canvas-edge-labels", "canvas-nodes"],
            cut.FindAll(".canvas-world > g").Select(group => group.ClassName));
    }

    [Fact]
    public void Should_RaiseSelection_When_ANodeIsClicked()
    {
        // Arrange
        var story = new Story("The Wreck");
        var fork = story.AddScene(SceneKind.Choice, "A fork");
        SceneId? selected = null;
        var cut = RenderCanvas(story, onSelected: id => selected = id);

        // Act
        NodeFor(cut, fork.Id).Click();

        // Assert
        Assert.Equal(fork.Id, selected);
    }

    [Fact]
    public void Should_HighlightTheSelectedScene_When_OneIsSelected()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var fork = story.AddScene(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderCanvas(story, selected: fork.Id);

        // Assert
        Assert.Contains("node-selected", NodeFor(cut, fork.Id).ClassList);
        Assert.DoesNotContain("node-selected", NodeFor(cut, storm.Id).ClassList);
    }

    [Fact]
    public async Task Should_UseTheSavedPosition_When_OneExists()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(412.5, 96));

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Equal("translate(412.5 96)", NodeFor(cut, storm.Id).GetAttribute("transform"));
    }

    [Fact]
    public void Should_PlaceTheScene_When_NoPositionIsSaved()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var deck = story.AddScene(SceneKind.Linear, "Below deck");
        story.SetFollowUp(storm.Id, deck.Id);
        story.SetStartScene(storm.Id);

        // Act
        var cut = RenderCanvas(story);

        // Assert — the start scene opens the first column and its follow-up the next.
        Assert.Equal("translate(40 40)", NodeFor(cut, storm.Id).GetAttribute("transform"));
        Assert.Equal("translate(360 40)", NodeFor(cut, deck.Id).GetAttribute("transform"));
    }

    [Fact]
    public void Should_PlaceASceneAddedLater_When_TheCanvasRendersAgain()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);
        story.AddScene(SceneKind.Linear, "Below deck");

        // Act — the page re-renders the canvas with the same story after every edit.
        cut.Render(parameters => parameters.Add(p => p.Story, story));

        // Assert
        Assert.Equal(2, cut.FindAll(".canvas-node").Count);
    }

    [Fact]
    public async Task Should_ReloadPositions_When_ADifferentStoryIsShown()
    {
        // Arrange
        var first = new Story("The Wreck");
        first.AddScene(SceneKind.Linear, "A storm gathers");
        var second = new Story("Shadows of Kell");
        var gate = second.AddScene(SceneKind.Linear, "The city gate");
        var secondLayout = new FakeSceneLayoutStore();
        await SavePositionAsync(secondLayout, gate.Id, new ScenePosition(700, 300));
        var cut = RenderCanvas(first);

        // Act
        cut.Render(parameters => parameters
            .Add(p => p.Story, second)
            .Add(p => p.Layout, secondLayout));

        // Assert
        Assert.Equal("translate(700 300)", NodeFor(cut, gate.Id).GetAttribute("transform"));
        Assert.Equal(1, secondLayout.LoadCount);
    }

    [Fact]
    public async Task Should_KeepTheNewStorysPositions_When_TheOldStorysLoadFinishesLate()
    {
        // Arrange — the first story's positions are still loading when the second story is shown.
        var first = new Story("The Wreck");
        var storm = first.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(900, 900));
        _layout.LoadGate = new TaskCompletionSource();
        var second = new Story("Shadows of Kell");
        var gate = second.AddScene(SceneKind.Linear, "The city gate");
        var secondLayout = new FakeSceneLayoutStore();
        await SavePositionAsync(secondLayout, gate.Id, new ScenePosition(700, 300));
        var cut = RenderCanvas(first);
        cut.Render(parameters => parameters
            .Add(p => p.Story, second)
            .Add(p => p.Layout, secondLayout));

        // Act
        await cut.InvokeAsync(() => _layout.LoadGate.SetResult());

        // Assert
        Assert.Equal("translate(700 300)", NodeFor(cut, gate.Id).GetAttribute("transform"));
        Assert.Single(cut.FindAll(".canvas-node"));
    }

    [Fact]
    public async Task Should_DrawNoNodes_When_PositionsAreStillLoading()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(900, 900));
        _layout.LoadGate = new TaskCompletionSource();

        // Act — the page re-renders with the same story before its positions arrive.
        var cut = RenderCanvas(story);
        cut.Render(parameters => parameters.Add(p => p.Story, story));

        // Assert — an auto-placed map now would jump when the saved one lands.
        Assert.Empty(cut.FindAll(".canvas-node"));
    }

    [Fact]
    public async Task Should_DrawTheSavedPositions_When_TheyArriveAfterTheCanvasRenderedAgain()
    {
        // Arrange — a re-render while the read is in flight must not cost the read its result.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(900, 900));
        _layout.LoadGate = new TaskCompletionSource();
        var cut = RenderCanvas(story);
        cut.Render(parameters => parameters.Add(p => p.Story, story));

        // Act
        await cut.InvokeAsync(() => _layout.LoadGate.SetResult());

        // Assert
        Assert.Equal("translate(900 900)", NodeFor(cut, storm.Id).GetAttribute("transform"));
    }

    [Fact]
    public async Task Should_NotReadPositions_When_ThePrerenderHandedThemOver()
    {
        // Arrange — prerendering renders the canvas twice per page load. The second pass takes the
        // positions the first pass persisted instead of reading the story database again. The store
        // holds a different position, so reading it would be visible.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var deck = story.AddScene(SceneKind.Linear, "Below deck");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(900, 900));
        await SeedHandedOverLayoutAsync(new Dictionary<Guid, ScenePosition?>
        {
            [storm.Id.Value] = new ScenePosition(412.5, 96),
            [deck.Id.Value] = null,
        });

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Equal(0, _layout.LoadCount);
        Assert.Equal("translate(412.5 96)", NodeFor(cut, storm.Id).GetAttribute("transform"));
    }

    [Fact]
    public async Task Should_ReadPositions_When_TheHandedOverLayoutIsForAnotherStory()
    {
        // Arrange — the workspace is shared, so another tab can open a different story between the
        // prerender and this pass. Positions handed over for different scenes are not this story's.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(900, 900));
        await SeedHandedOverLayoutAsync(new Dictionary<Guid, ScenePosition?>
        {
            [Guid.NewGuid()] = new ScenePosition(412.5, 96),
        });

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Equal(1, _layout.LoadCount);
        Assert.Equal("translate(900 900)", NodeFor(cut, storm.Id).GetAttribute("transform"));
    }

    [Fact]
    public async Task Should_CancelThePositionLoad_When_TheCanvasIsDisposed()
    {
        // Arrange — the story can close while its positions are still being read.
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        _layout.LoadGate = new TaskCompletionSource();
        RenderCanvas(story);

        // Act
        await DisposeComponentsAsync();

        // Assert
        Assert.True(_layout.LastLoadCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void Should_NotReloadPositions_When_TheSameStoryRendersAgain()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);

        // Act
        cut.Render(parameters => parameters.Add(p => p.Story, story));

        // Assert — positions are read once per story, not once per edit.
        Assert.Equal(1, _layout.LoadCount);
    }

    [Fact]
    public async Task Should_NotSaveAnything_When_OnlyRendering()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        story.AddScene(SceneKind.Linear, "Below deck");

        // Act
        RenderCanvas(story, selected: storm.Id);

        // Assert — placed positions are the canvas's own; only a creator's drag writes one (M21).
        Assert.Equal(0, _layout.SaveCount);
        Assert.Empty(await _layout.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_ShowTheEmptyState_When_StoryHasNoScenes()
    {
        // Arrange
        var story = new Story("The Wreck");

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Equal(
            "This story has no scenes yet. Add the one it opens with, in the navigator on the right.",
            cut.Find(".canvas .canvas-empty").TextContent.Trim());
        Assert.Empty(cut.FindAll(".canvas-svg"));
    }

    [Fact]
    public void Should_ShowNoEmptyState_When_TheStoryHasScenes()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderCanvas(story);

        // Assert
        Assert.Empty(cut.FindAll(".canvas-empty"));
    }

    [Fact]
    public void Should_ReportTheFailureAndPlaceEveryScene_When_PositionsCannotBeLoaded()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        _layout.FailNextLoad = new IOException("The story folder can't be read.");

        // Act
        var cut = RenderCanvas(story);

        // Assert — the map still shows, laid out afresh, and says why it may not look as left.
        Assert.Contains("can't be read", cut.Find(".canvas-error").TextContent, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".canvas-node"));
    }

    [Fact]
    public async Task Should_FillTheRegion_When_Rendered()
    {
        // Arrange — the map slides under the window instead of scrolling, so the drawing takes the
        // region's size from the stylesheet, not its content's size from the markup.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(1000, 500.5));

        // Act
        var cut = RenderCanvas(story);

        // Assert
        var svg = cut.Find(".canvas-svg");
        Assert.Null(svg.GetAttribute("width"));
        Assert.Null(svg.GetAttribute("height"));
    }

    [Fact]
    public void Should_ImportTheModule_When_FirstRendered()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        RenderCanvas(story);

        // Assert
        JSInterop.VerifyInvoke("import");
        _module.VerifyInvoke("attach");
    }

    [Fact]
    public async Task Should_DetachTheShim_When_Disposed()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        RenderCanvas(story);

        // Act
        await DisposeComponentsAsync();

        // Assert
        _module.VerifyInvoke("detach");
    }

    [Fact]
    public void Should_PlaceTheMapAtActualSize_When_Rendered()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderCanvas(story);

        // Assert — the frame M20 drew, until the creator slides or zooms it.
        Assert.Equal("translate(0 0) scale(1)", WorldTransform(cut));
        Assert.Equal("0", cut.Find(".canvas-svg").GetAttribute("tabindex"));
    }

    [Fact]
    public async Task Should_SlideTheMap_When_TheGroundIsDragged()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);

        // Act
        cut.Find(".canvas-svg").PointerDown(Press(100, 100));
        await cut.Instance.MoveAsync(130, 90);

        // Assert — the map follows the pointer, and the ground says it is being held.
        Assert.Equal("translate(30 -10) scale(1)", WorldTransform(cut));
        Assert.Contains("canvas-panning", cut.Find(".canvas").ClassList);

        await cut.Instance.UpAsync(130, 90);
        Assert.DoesNotContain("canvas-panning", cut.Find(".canvas").ClassList);
    }

    [Fact]
    public async Task Should_NotSlideTheMap_When_ASecondaryButtonPressesTheGround()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);

        // Act
        cut.Find(".canvas-svg").PointerDown(Press(100, 100, button: 2));
        await cut.Instance.MoveAsync(130, 90);

        // Assert
        Assert.Equal("translate(0 0) scale(1)", WorldTransform(cut));
    }

    [Fact]
    public async Task Should_IgnoreMoves_When_NothingIsPressed()
    {
        // Arrange — the shim forwards moves for any captured pointer; the canvas decides.
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);

        // Act
        await cut.Instance.MoveAsync(500, 500);

        // Assert
        Assert.Equal("translate(0 0) scale(1)", WorldTransform(cut));
    }

    [Fact]
    public async Task Should_ZoomAboutTheCursor_When_TheWheelTurns()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);

        // Act — one notch towards the creator zooms in one step, about (100, 100).
        await cut.Instance.ZoomAsync(100, 100, deltaY: -100);

        // Assert
        Assert.Equal("translate(-20 -20) scale(1.2)", WorldTransform(cut));
    }

    [Theory]
    [InlineData("ArrowRight", "translate(-40 0) scale(1)")]
    [InlineData("ArrowLeft", "translate(40 0) scale(1)")]
    [InlineData("ArrowDown", "translate(0 -40) scale(1)")]
    [InlineData("ArrowUp", "translate(0 40) scale(1)")]
    public void Should_SlideTheMapWithTheArrowKeys_When_TheMapHasFocus(string key, string expectedTransform)
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);

        // Act — the arrow names where the creator wants to look, so the map slides the other way.
        cut.Find(".canvas-svg").KeyDown(new KeyboardEventArgs { Key = key });

        // Assert
        Assert.Equal(expectedTransform, WorldTransform(cut));
    }

    [Theory]
    [InlineData("+", "translate(-80 -60) scale(1.2)")]
    [InlineData("=", "translate(-80 -60) scale(1.2)")]
    [InlineData("-", "translate(66.67 50) scale(0.8333)")]
    public async Task Should_ZoomAboutTheWindowsCentre_When_PlusOrMinusIsPressed(string key, string expectedTransform)
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);
        await cut.Instance.ResizeAsync(800, 600);

        // Act
        cut.Find(".canvas-svg").KeyDown(new KeyboardEventArgs { Key = key });

        // Assert
        Assert.Equal(expectedTransform, WorldTransform(cut));
    }

    [Fact]
    public async Task Should_SlideTheMapWithTheArrowKeys_When_ASceneHasFocus()
    {
        // Arrange — a keyboard creator moves from scene to scene with Tab; the arrows still slide.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);
        await cut.Instance.ResizeAsync(800, 600);

        // Act
        NodeFor(cut, storm.Id).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Assert
        Assert.Equal("translate(-40 0) scale(1)", WorldTransform(cut));
    }

    [Fact]
    public async Task Should_OpenAtActualSize_When_TheWholeStoryFitsTheWindow()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderCanvas(story);

        // Act — the browser reports the window's size once the circuit attaches.
        await cut.Instance.ResizeAsync(800, 600);

        // Assert — the story sits at the page's origin, as M20 drew it.
        Assert.Equal("translate(0 0) scale(1)", WorldTransform(cut));
    }

    [Fact]
    public async Task Should_ShowTheWholeStory_When_ItDoesNotFitTheWindowOnOpen()
    {
        // Arrange — a scene dragged far from the origin would otherwise open to a blank window.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(1000, 500));
        var cut = RenderCanvas(story);

        // Act
        await cut.Instance.ResizeAsync(800, 600);

        // Assert — framed and centred; it fits at actual size, so no zoom.
        Assert.Equal("translate(-700 -232) scale(1)", WorldTransform(cut));
    }

    [Fact]
    public async Task Should_KeepTheView_When_TheWindowIsMeasuredAgain()
    {
        // Arrange — a story is framed once on open; a later resize never moves the map under the creator.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(1000, 500));
        var cut = RenderCanvas(story);
        await cut.Instance.ResizeAsync(800, 600);
        cut.Find(".canvas-svg").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Act
        await cut.Instance.ResizeAsync(1200, 900);

        // Assert
        Assert.Equal("translate(-740 -232) scale(1)", WorldTransform(cut));
    }

    [Fact]
    public async Task Should_FrameTheStory_When_ItsPositionsArriveAfterTheWindowWasMeasured()
    {
        // Arrange — the window can be measured while the positions are still being read.
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(1000, 500));
        _layout.LoadGate = new TaskCompletionSource();
        var cut = RenderCanvas(story);
        await cut.Instance.ResizeAsync(800, 600);

        // Act
        await cut.InvokeAsync(() => _layout.LoadGate.SetResult());

        // Assert
        Assert.Equal("translate(-700 -232) scale(1)", WorldTransform(cut));
    }

    [Fact]
    public async Task Should_ResetTheView_When_ADifferentStoryIsShown()
    {
        // Arrange
        var first = new Story("The Wreck");
        first.AddScene(SceneKind.Linear, "A storm gathers");
        var second = new Story("Shadows of Kell");
        second.AddScene(SceneKind.Linear, "The city gate");
        var cut = RenderCanvas(first);
        await cut.Instance.ResizeAsync(800, 600);
        await cut.Instance.ZoomAsync(100, 100, deltaY: -100);

        // Act
        cut.Render(parameters => parameters
            .Add(p => p.Story, second)
            .Add(p => p.Layout, new FakeSceneLayoutStore()));

        // Assert — the new story opens the way any story opens, not through the old one's zoom.
        Assert.Equal("translate(0 0) scale(1)", WorldTransform(cut));
    }

    private static string? WorldTransform(IRenderedComponent<StoryCanvas> cut) =>
        cut.Find(".canvas-world").GetAttribute("transform");

    private static PointerEventArgs Press(double clientX, double clientY, long button = 0) =>
        new() { Button = button, PointerId = 1, ClientX = clientX, ClientY = clientY };

    private static AngleSharp.Dom.IElement NodeFor(IRenderedComponent<StoryCanvas> cut, SceneId sceneId) =>
        cut.Find($".canvas-node[data-scene-id='{sceneId.Value}']");

    // Seeds the state a prerender would have handed over, serialized the way the framework does.
    // Only the restore half is exercised: PersistStateAsync needs a Renderer, which bUnit does not
    // surface, so the persisting callback stays an accepted boundary (overlays/frontend-blazor.md),
    // as it is for the picker's recent list in HomeTests.
    private Task SeedHandedOverLayoutAsync(Dictionary<Guid, ScenePosition?> layout) =>
        _persistence.RestoreStateAsync(new SeededStore(new Dictionary<string, byte[]>
        {
            [LayoutStateKey] = JsonSerializer.SerializeToUtf8Bytes(layout, JsonSerializerOptions.Web),
        }));

    private static Task SavePositionAsync(FakeSceneLayoutStore layout, SceneId sceneId, ScenePosition position) =>
        layout.SaveAsync(
            new Dictionary<SceneId, ScenePosition> { [sceneId] = position },
            TestContext.Current.CancellationToken);

    private IRenderedComponent<StoryCanvas> RenderCanvas(
        Story story, SceneId? selected = null, Action<SceneId?>? onSelected = null) =>
        Render<StoryCanvas>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.Layout, _layout)
            .Add(p => p.SelectedSceneId, selected)
            .Add(p => p.SelectedSceneIdChanged, onSelected ?? (_ => { })));

    private sealed class SeededStore(IDictionary<string, byte[]> state) : IPersistentComponentStateStore
    {
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult(state);

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> instance) => Task.CompletedTask;
    }
}
