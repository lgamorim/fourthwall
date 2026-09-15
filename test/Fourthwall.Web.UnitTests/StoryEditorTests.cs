using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Infrastructure;

using Fourthwall.Web.Components.Canvas;

using Bunit.TestDoubles;

using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fourthwall.Web.UnitTests;

public class StoryEditorTests : BunitContext
{
    private readonly FakeStoryWorkspace _workspace = new();
    private readonly FakeStoryValidation _validation = new();

    public StoryEditorTests()
    {
        Services.AddSingleton<IStoryWorkspace>(_workspace);
        Services.AddSingleton<IStoryValidation>(_validation);
        Services.AddSingleton<IStoryGraphFactory>(new Graph1xStoryGraphFactory());

        // The host registers PersistentComponentState as part of AddRazorComponents; bUnit does not,
        // and the canvas hands its positions across the prerender with it.
        Services.AddSingleton(new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance).State);

        // The canvas imports its shim on first render; the shim is an accepted untestable boundary.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Should_ReturnToThePicker_When_NoStoryIsOpen()
    {
        // Arrange & Act
        RenderEditor();

        // Assert
        Assert.Equal("/", Assert.Single(Navigation.History).Uri);
    }

    [Fact]
    public async Task Should_ShowTheSceneList_When_AStoryIsOpen()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert
        Assert.NotNull(cut.Find(".scene-create"));
    }

    [Fact]
    public async Task Should_ShowTheNavigatorInTheDock_When_AStoryIsOpen()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert — the scene list lives in the dock as the navigator, between the story-level
        // validation panel and the inspector, in that order.
        Assert.NotNull(cut.Find(".app-dock .navigator .scene-create"));
        Assert.Equal(
            ["validation", "navigator", "inspector"],
            cut.FindAll(".app-dock > section").Select(section => section.ClassName));
    }

    [Fact]
    public async Task Should_InviteToAddTheFirstScene_When_TheStoryHasNoScenes()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert — nothing to draw yet, so the canvas points at the one thing to do, by its name.
        Assert.Equal(
            "This story has no scenes yet. Choose Add scene above to write the one it opens with.",
            cut.Find(".canvas .canvas-empty").TextContent.Trim());
        Assert.Equal("Add scene", cut.Find(".editor-toolbar #canvas-add-scene").TextContent.Trim());
    }

    [Fact]
    public async Task Should_SaveAndOpenTheNewScene_When_AddSceneIsClickedInTheToolbar()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var cut = RenderEditor();
        await cut.FindComponent<StoryCanvas>().Instance.ResizeAsync(800, 600);

        // Act
        await cut.Find("#canvas-add-scene").ClickAsync(new MouseEventArgs());

        // Assert — saved before its place, selected, and open in the inspector with its prompt.
        var added = Assert.Single(story.Scenes);
        Assert.Equal(1, _workspace.SaveCount);
        Assert.Equal(1, _workspace.LayoutStore.SaveCount);
        Assert.Contains("node-selected", NodeFor(cut, added.Id).ClassList);
        Assert.Equal("What happens in this scene?", cut.Find("#inspector-text").GetAttribute("placeholder"));
    }

    [Fact]
    public async Task Should_ShowTheTransitionsEditor_When_ALinkIsWiredOnTheCanvas()
    {
        // Arrange — the fork opens the first column at (40, 40); the deck is placed at (360, 40).
        var story = await OpenStoryAsync();
        var fork = story.AddScene(SceneKind.Choice, "A fork");
        story.AddScene(SceneKind.Linear, "Below deck");
        story.SetStartScene(fork.Id);
        var cut = RenderEditor();
        var canvas = cut.FindComponent<StoryCanvas>().Instance;

        // Act — from the fork's port (240, 72) into the deck.
        cut.Find($".canvas-node[data-scene-id='{fork.Id.Value}'] .node-port")
            .PointerDown(new PointerEventArgs { Button = 0, PointerId = 1, ClientX = 100, ClientY = 100 });
        await canvas.MoveAsync(240, 100);
        await canvas.UpAsync(240, 100);

        // Assert — saved, and the new row waits in the dock to be renamed.
        Assert.Equal(1, _workspace.SaveCount);
        Assert.Equal("Name this choice", cut.Find(".choice-row .choice-label").GetAttribute("value"));
        Assert.Contains("node-selected", NodeFor(cut, fork.Id).ClassList);
    }

    [Fact]
    public async Task Should_ShowTheCanvas_When_AStoryWithScenesIsOpen()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderEditor();

        // Assert — in the main region, not the dock.
        var node = cut.Find($".canvas-node[data-scene-id='{scene.Id.Value}']");
        Assert.Null(node.Closest(".app-dock"));
    }

    [Fact]
    public async Task Should_ShowTheInspector_When_ANodeIsClicked()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var scene = story.AddScene(SceneKind.Choice, "A fork");
        var cut = RenderEditor();

        // Act
        NodeFor(cut, scene.Id).Click();

        // Assert — the canvas and the navigator select through the same page field.
        Assert.NotNull(cut.Find("#inspector-text"));
        Assert.Contains("scene-row-selected", cut.Find($".scene-row[data-scene-id='{scene.Id.Value}']").ClassList);
    }

    [Fact]
    public async Task Should_HighlightTheNode_When_ASceneIsPickedInTheNavigator()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var fork = story.AddScene(SceneKind.Choice, "A fork");
        var cut = RenderEditor();

        // Act
        cut.Find($".scene-row[data-scene-id='{fork.Id.Value}'] .scene-select").Click();

        // Assert
        Assert.Contains("node-selected", NodeFor(cut, fork.Id).ClassList);
        Assert.DoesNotContain("node-selected", NodeFor(cut, storm.Id).ClassList);
    }

    [Fact]
    public async Task Should_HighlightTheNode_When_AValidationChipIsClicked()
    {
        // Arrange
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var orphan = story.AddScene(SceneKind.Linear, "Adrift");
        _validation.Report = new ValidationReport(
        [
            new ValidationViolation(
                ValidationRule.AllScenesReachable, ValidationSeverity.Error, "1 scene cannot be reached.", [orphan.Id]),
        ]);
        var cut = RenderEditor();
        cut.Find("#validate").Click();

        // Act
        cut.Find(".validation-scene").Click();

        // Assert
        Assert.Contains("node-selected", NodeFor(cut, orphan.Id).ClassList);
    }

    [Fact]
    public async Task Should_SaveTheStory_When_ASceneIsAdded()
    {
        // Arrange
        await OpenStoryAsync();
        var cut = RenderEditor();
        cut.Find("#scene-create-text").Change("A storm gathers");

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert — every committed mutation is persisted; there is no explicit save.
        Assert.Equal(1, _workspace.SaveCount);
    }

    [Fact]
    public async Task Should_RenameTheStory_When_ANewTitleIsCommitted()
    {
        // Arrange
        await OpenStoryAsync();
        var cut = RenderEditor();

        // Act
        cut.Find("#story-title").Change("The Wreck of the Marianne");

        // Assert
        Assert.Equal("The Wreck of the Marianne", _workspace.Current!.Title);
        Assert.Equal(1, _workspace.SaveCount);
    }

    [Fact]
    public async Task Should_RefuseABlankTitle_When_ItIsCommitted()
    {
        // Arrange — Story.Rename rejects a blank title, so the page must not hand it one.
        await OpenStoryAsync();
        var cut = RenderEditor();

        // Act
        cut.Find("#story-title").Change("   ");

        // Assert
        Assert.Equal("The Wreck", _workspace.Current!.Title);
        Assert.NotNull(cut.Find(".editor-error"));
    }

    [Fact]
    public async Task Should_ShowTheInspector_When_ASceneIsSelected()
    {
        // Arrange
        await OpenStoryAsync();
        var cut = RenderEditor();
        cut.Find("#scene-create-text").Change("A storm gathers");
        cut.Find("#scene-create-submit").Click();

        // Act
        cut.Find(".scene-select").Click();

        // Assert — a newly added scene is selected, and the dock shows it.
        Assert.NotNull(cut.Find("#inspector-text"));
    }

    [Fact]
    public async Task Should_ShowTheTransitionsEditor_When_AChoiceSceneIsSelected()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var scene = story.AddScene(SceneKind.Choice, "A fork");
        story.AddScene(SceneKind.Linear, "left");
        var cut = RenderEditor();

        // Act
        cut.FindAll(".scene-row").Single(row => row.TextContent.Contains("A fork", StringComparison.Ordinal))
            .QuerySelector(".scene-select")!.Click();

        // Assert
        Assert.NotNull(cut.Find("#choice-add-submit"));
    }

    [Fact]
    public async Task Should_SaveTheStory_When_AChoiceIsWired()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var scene = story.AddScene(SceneKind.Choice, "A fork");
        story.AddScene(SceneKind.Linear, "left");
        var cut = RenderEditor();
        cut.FindAll(".scene-row").Single(row => row.TextContent.Contains("A fork", StringComparison.Ordinal))
            .QuerySelector(".scene-select")!.Click();
        cut.Find("#choice-add-label").Change("Go left");

        // Act
        cut.Find("#choice-add-submit").Click();

        // Assert
        Assert.Single(scene.Choices);
        Assert.Equal(1, _workspace.SaveCount);
    }

    [Fact]
    public async Task Should_ShowNoInspector_When_NothingIsSelected()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert
        Assert.Empty(cut.FindAll("#inspector-text"));
    }

    [Fact]
    public async Task Should_ReportTheFailure_When_SavingThrows()
    {
        // Arrange
        await OpenStoryAsync();
        _workspace.FailNextSave = new IOException("The story folder is read-only.");
        var cut = RenderEditor();
        cut.Find("#scene-create-text").Change("A storm gathers");

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert
        Assert.Contains("read-only", cut.Find(".editor-error").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_NotCrash_When_TheStoryClosesBeforeARenameCommits()
    {
        // Arrange — the browser can deliver a commit for a render that is already stale: the header
        // in this tab, or a second tab, can close the story first.
        await OpenStoryAsync();
        var cut = RenderEditor();
        var title = cut.Find("#story-title");
        await _workspace.CloseAsync(TestContext.Current.CancellationToken);

        // Act
        var exception = Record.Exception(() => title.Change("The Wreck of the Marianne"));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Should_EmptyTheDock_When_TheLayoutRendersAfterTheStoryCloses()
    {
        // Arrange — closing the story sends this page to the picker without re-rendering it, but
        // the layout re-renders on the same Changed event, and its outlet runs the dock fragment
        // again. The page's outer check never sees that render, so the fragment must check itself.
        await OpenStoryAsync();
        var cut = RenderEditor();
        await _workspace.CloseAsync(TestContext.Current.CancellationToken);

        // Act — the host stands in for MainLayout re-rendering its outlet.
        var exception = Record.Exception(() => cut.Render());

        // Assert
        Assert.Null(exception);
        Assert.Empty(cut.FindAll(".app-dock > *"));
    }

    [Fact]
    public async Task Should_ReturnToThePicker_When_TheStoryIsClosed()
    {
        // Arrange
        await OpenStoryAsync();
        RenderEditor();

        // Act
        await _workspace.CloseAsync(TestContext.Current.CancellationToken);

        // Assert — the header can close the story while the editor is showing it.
        Assert.Equal("/", Assert.Single(Navigation.History).Uri);
    }

    [Fact]
    public async Task Should_KeepTheValidationReport_When_AValidationChipIsClicked()
    {
        // Arrange
        var orphan = await OpenStoryWithAReportNamingASceneAsync();
        var cut = RenderEditor();
        cut.Find("#validate").Click();

        // Act
        cut.Find(".validation-scene").Click();

        // Assert — picking a scene saves nothing, so the report still describes the story.
        Assert.Single(cut.FindAll(".validation-violation"));
        Assert.Equal(0, _workspace.SaveCount);
        Assert.Contains("scene-row-selected", cut.Find($".scene-row[data-scene-id='{orphan.Id.Value}']").ClassList);
    }

    [Fact]
    public async Task Should_KeepTheValidationReport_When_ASceneIsPickedInTheNavigator()
    {
        // Arrange
        await OpenStoryWithAReportNamingASceneAsync();
        var cut = RenderEditor();
        cut.Find("#validate").Click();

        // Act
        cut.Find(".scene-select").Click();

        // Assert
        Assert.Single(cut.FindAll(".validation-violation"));
    }

    [Fact]
    public async Task Should_KeepTheSceneDraft_When_ASceneIsSelected()
    {
        // Arrange — the page re-renders on every selection; the dock's own state must outlive that.
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderEditor();
        cut.Find("#scene-create-text").Change("A half-written scene");

        // Act
        cut.Find(".scene-select").Click();

        // Assert
        Assert.Equal("A half-written scene", cut.Find("#scene-create-text").GetAttribute("value"));
    }

    [Fact]
    public async Task Should_DropTheValidationReport_When_AnEditIsSaved()
    {
        // Arrange — a report of the story as it used to be would mislead, so a saved edit clears it.
        await OpenStoryWithAReportNamingASceneAsync();
        var cut = RenderEditor();
        cut.Find("#validate").Click();
        cut.Find(".scene-select").Click();

        // Act
        cut.Find("#inspector-text").Change("The storm breaks");

        // Assert
        Assert.Equal(1, _workspace.SaveCount);
        Assert.Empty(cut.FindAll(".validation-violation"));
        Assert.NotNull(cut.Find(".validation-idle"));
    }

    [Fact]
    public async Task Should_KeepTheValidationReport_When_ANodeIsDragged()
    {
        // Arrange — moving a page changes nothing about the story, so the report still describes it.
        var orphan = await OpenStoryWithAReportNamingASceneAsync();
        var cut = RenderEditor();
        cut.Find("#validate").Click();

        // Act
        await DragNodeAsync(cut, orphan.Id);

        // Assert
        Assert.Single(cut.FindAll(".validation-violation"));
    }

    [Fact]
    public async Task Should_NotSaveTheStory_When_ANodeIsDragged()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderEditor();

        // Act
        await DragNodeAsync(cut, storm.Id);

        // Assert — the position goes to the layout store; the story is not rewritten.
        Assert.Equal(0, _workspace.SaveCount);
        Assert.Equal(1, _workspace.LayoutStore.SaveCount);
    }

    [Fact]
    public async Task Should_ShowTheWholeStory_When_TheToolbarSaysSo()
    {
        // Arrange
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderEditor();
        var canvas = cut.FindComponent<StoryCanvas>().Instance;
        await canvas.ResizeAsync(800, 600);
        await canvas.ZoomAsync(0, 0, deltaY: 300);

        // Act
        cut.Find("#canvas-fit").Click();

        // Assert
        Assert.Equal("translate(260 228) scale(1)", cut.Find(".canvas-world").GetAttribute("transform"));
    }

    [Fact]
    public async Task Should_RestoreActualSize_When_TheToolbarSaysSo()
    {
        // Arrange
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderEditor();
        var canvas = cut.FindComponent<StoryCanvas>().Instance;
        await canvas.ResizeAsync(800, 600);
        await canvas.ZoomAsync(100, 100, deltaY: -100);

        // Act
        cut.Find("#canvas-reset").Click();

        // Assert
        Assert.Equal("translate(0 0) scale(1)", cut.Find(".canvas-world").GetAttribute("transform"));
    }

    [Fact]
    public async Task Should_NameTheViewControlsForTheCreator_When_AStoryIsOpen()
    {
        // Arrange
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderEditor();

        // Assert — words at the toolbar's right edge (design note §11.1), enabled with scenes to show.
        var fit = cut.Find(".editor-toolbar .canvas-controls #canvas-fit");
        var reset = cut.Find(".editor-toolbar .canvas-controls #canvas-reset");
        Assert.Equal("Show whole story", fit.TextContent.Trim());
        Assert.Equal("Actual size", reset.TextContent.Trim());
        Assert.False(fit.HasAttribute("disabled"));
        Assert.False(reset.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Should_DisableTheViewControls_When_TheStoryHasNoScenes()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert — nothing to show or to size.
        Assert.True(cut.Find("#canvas-fit").HasAttribute("disabled"));
        Assert.True(cut.Find("#canvas-reset").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Should_DisableAddScene_When_ThePositionsAreStillLoading()
    {
        // Arrange — until the story's places on the map arrive there is no map to add a scene to,
        // which is better said than discovered (design note §12.4).
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");
        _workspace.LayoutStore.LoadGate = new TaskCompletionSource();

        // Act
        var cut = RenderEditor();

        // Assert
        Assert.True(cut.Find("#canvas-add-scene").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Should_EnableAddScene_When_ThePositionsHaveLoaded()
    {
        // Arrange
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");
        _workspace.LayoutStore.LoadGate = new TaskCompletionSource();
        var cut = RenderEditor();

        // Act
        await cut.InvokeAsync(() => _workspace.LayoutStore.LoadGate.SetResult());

        // Assert
        Assert.False(cut.Find("#canvas-add-scene").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Should_EnableAddScene_When_AnEmptyStoryIsOpen()
    {
        // Arrange — an empty story has no positions to wait for once the read returns.
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert — the invitation points at it, so it must work.
        Assert.False(cut.Find("#canvas-add-scene").HasAttribute("disabled"));
    }

    private static async Task DragNodeAsync(IRenderedComponent<DockHost> cut, SceneId sceneId)
    {
        var canvas = cut.FindComponent<StoryCanvas>().Instance;
        NodeFor(cut, sceneId).PointerDown(new PointerEventArgs { Button = 0, PointerId = 1, ClientX = 100, ClientY = 100 });
        await canvas.MoveAsync(130, 120);
        await canvas.UpAsync(130, 120);
    }

    private async Task<Scene> OpenStoryWithAReportNamingASceneAsync()
    {
        var story = await OpenStoryAsync();
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var orphan = story.AddScene(SceneKind.Linear, "Adrift");
        _validation.Report = new ValidationReport(
        [
            new ValidationViolation(
                ValidationRule.AllScenesReachable, ValidationSeverity.Error, "1 scene cannot be reached.", [orphan.Id]),
        ]);
        return orphan;
    }

    private static AngleSharp.Dom.IElement NodeFor(IRenderedComponent<DockHost> cut, SceneId sceneId) =>
        cut.Find($".canvas-node[data-scene-id='{sceneId.Value}']");

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private IRenderedComponent<DockHost> RenderEditor() => Render<DockHost>();

    private Task<Story> OpenStoryAsync() =>
        _workspace.CreateAsync(@"C:\stories\wreck", "The Wreck", TestContext.Current.CancellationToken);
}
