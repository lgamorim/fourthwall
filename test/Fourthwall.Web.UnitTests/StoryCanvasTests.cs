using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Infrastructure;
using Fourthwall.Web.Components.Canvas;
using Fourthwall.Web.Composition;

using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class StoryCanvasTests : BunitContext
{
    private readonly FakeSceneLayoutStore _layout = new();

    public StoryCanvasTests()
    {
        Services.AddSingleton<IStoryGraphFactory>(new Graph1xStoryGraphFactory());
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
            new[] { storm.Id, fork.Id }.Select(id => id.Value.ToString()).Order(),
            cut.FindAll(".canvas-node").Select(node => node.GetAttribute("data-scene-id")!).Order());
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
            NodeFor(cut, storm.Id).QuerySelector(".node-thumbnail image")!.GetAttribute("href"));
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
        await cut.InvokeAsync(() => _layout.LoadGate.SetResult());
        Assert.Equal("translate(900 900)", NodeFor(cut, storm.Id).GetAttribute("transform"));
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
    public async Task Should_SizeTheDrawingToItsContent_When_Rendered()
    {
        // Arrange
        var story = new Story("The Wreck");
        var storm = story.AddScene(SceneKind.Linear, "A storm gathers");
        await SavePositionAsync(_layout, storm.Id, new ScenePosition(1000, 500.5));

        // Act
        var cut = RenderCanvas(story);

        // Assert — until pan arrives the canvas scrolls, so the drawing must reach its furthest node.
        var svg = cut.Find(".canvas-svg");
        Assert.Equal(CanvasGeometry.Invariant(1000 + CanvasGeometry.NodeWidth + CanvasGeometry.ContentMargin), svg.GetAttribute("width"));
        Assert.Equal(CanvasGeometry.Invariant(500.5 + CanvasGeometry.NodeHeight + CanvasGeometry.ContentMargin), svg.GetAttribute("height"));
    }

    private static AngleSharp.Dom.IElement NodeFor(IRenderedComponent<StoryCanvas> cut, SceneId sceneId) =>
        cut.Find($".canvas-node[data-scene-id='{sceneId.Value}']");

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
}
