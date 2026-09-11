using System.Globalization;
using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class CanvasModelTests
{
    [Fact]
    public void Should_AddOneEdge_When_ASceneHasAChoice()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.WireChoice(start.Id, "Walk on", target.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        var edge = Assert.Single(model.Edges);
        Assert.Equal(start.Id, edge.Source);
        Assert.Equal(target.Id, edge.Target);
        Assert.Equal("Walk on", edge.Label);
        Assert.Equal(new CanvasEdgeKey(start.Id, 0), edge.Key);
        Assert.False(edge.IsSelfLoop);
    }

    [Fact]
    public void Should_AddAnUnlabelledEdge_When_ASceneHasAFollowUp()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "A corridor");
        var target = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.SetFollowUp(start.Id, target.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        var edge = Assert.Single(model.Edges);
        Assert.Equal(string.Empty, edge.Label);
        Assert.Equal(new CanvasEdgeKey(start.Id, null), edge.Key);
    }

    [Fact]
    public void Should_SeparateParallelEdges_When_TwoChoicesShareATarget()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.WireChoice(start.Id, "Go left", target.Id);
        story.WireChoice(start.Id, "Go right", target.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        Assert.Equal(2, model.Edges.Count);
        Assert.Equal([0, 1], model.Edges.Select(edge => edge.ParallelIndex));
    }

    [Fact]
    public void Should_MarkASelfLoop_When_AChoiceTargetsItsOwnScene()
    {
        // Arrange
        var story = new Story("Story");
        var loop = story.AddScene(SceneKind.Choice, "A round room");
        story.WireChoice(loop.Id, "Keep walking", loop.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        var edge = Assert.Single(model.Edges);
        Assert.True(edge.IsSelfLoop);
    }

    [Fact]
    public void Should_HitTestTheTopmostNode_When_NodesOverlap()
    {
        // Scenes.Ordered sorts "A scene" before "B scene"; CanvasModel keeps that order in Nodes,
        // so the topmost (last-painted) node under an overlap is "B scene".

        // Arrange
        var story = new Story("Story");
        var first = story.AddScene(SceneKind.Linear, "A scene");
        var second = story.AddScene(SceneKind.Linear, "B scene");
        var shared = new ScenePosition(100, 100);
        var positions = new Dictionary<SceneId, ScenePosition> { [first.Id] = shared, [second.Id] = shared };

        // Act
        var model = CanvasModel.Build(story, positions);
        var hit = model.HitTest(new ScenePosition(150, 120));

        // Assert
        Assert.Equal(second.Id, hit);
    }

    [Fact]
    public void Should_ReturnNull_When_HitTestingEmptySpace()
    {
        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Linear, "Alone");
        var positions = new Dictionary<SceneId, ScenePosition> { [scene.Id] = new ScenePosition(0, 0) };

        // Act
        var model = CanvasModel.Build(story, positions);
        var hit = model.HitTest(new ScenePosition(-500, -500));

        // Assert
        Assert.Null(hit);
    }

    [Fact]
    public void Should_HitTest_When_PointSitsExactlyOnTheFarBoundary()
    {
        // The bounds are inclusive on every side. Pinning the exact edge (rather than only a point
        // well inside or well outside) means flipping either comparison to exclusive breaks this test.

        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Linear, "Alone");
        var positions = new Dictionary<SceneId, ScenePosition> { [scene.Id] = new ScenePosition(0, 0) };
        var model = CanvasModel.Build(story, positions);

        // Act
        var hit = model.HitTest(new ScenePosition(CanvasGeometry.NodeWidth, CanvasGeometry.NodeHeight));

        // Assert
        Assert.Equal(scene.Id, hit);
    }

    [Fact]
    public void Should_ReturnNull_When_PointSitsJustPastTheFarBoundary()
    {
        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Linear, "Alone");
        var positions = new Dictionary<SceneId, ScenePosition> { [scene.Id] = new ScenePosition(0, 0) };
        var model = CanvasModel.Build(story, positions);

        // Act
        var hit = model.HitTest(new ScenePosition(CanvasGeometry.NodeWidth + 0.01, CanvasGeometry.NodeHeight + 0.01));

        // Assert
        Assert.Null(hit);
    }

    [Fact]
    public void Should_ReturnAnEmptyModel_When_TheStoryHasNoScenes()
    {
        // Arrange
        var story = new Story("Story");
        var positions = new Dictionary<SceneId, ScenePosition>();

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        Assert.Empty(model.Nodes);
        Assert.Empty(model.Edges);
    }

    [Fact]
    public void Should_Throw_When_StoryIsNull()
    {
        // Arrange
        var positions = new Dictionary<SceneId, ScenePosition>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CanvasModel.Build(null!, positions));
    }

    [Fact]
    public void Should_Throw_When_PositionsIsNull()
    {
        // Arrange
        var story = new Story("Story");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CanvasModel.Build(story, null!));
    }

    [Fact]
    public void Should_ThrowNamingTheScene_When_ANodeHasNoPosition()
    {
        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Linear, "Alone");
        var positions = new Dictionary<SceneId, ScenePosition>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => CanvasModel.Build(story, positions));

        // Assert
        Assert.Contains(scene.Id.Value.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_ThrowNamingTheScene_When_AnEdgeTargetHasNoPosition()
    {
        // The scene at the other end of a transition is looked up too, whether or not the caller
        // remembered it — the missing scene here is never a node itself, only a choice's target.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "start");
        var target = story.AddScene(SceneKind.Ending, "end", EndingOutcome.Victory());
        story.WireChoice(start.Id, "Go", target.Id);
        var positions = new Dictionary<SceneId, ScenePosition> { [start.Id] = new ScenePosition(0, 0) };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => CanvasModel.Build(story, positions));

        // Assert
        Assert.Contains(target.Id.Value.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_FormatNodeTransformInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-PT");
        try
        {
            var story = new Story("Story");
            var scene = story.AddScene(SceneKind.Linear, "Alone");
            var node = new CanvasNode(scene, new ScenePosition(10.5, 20), IsStart: false);

            // Act
            var transform = node.Transform;

            // Assert
            Assert.Equal("translate(10.5 20)", transform);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static Dictionary<SceneId, ScenePosition> PositionEachSceneInOrder(Story story)
    {
        var positions = new Dictionary<SceneId, ScenePosition>();
        var x = 0.0;
        foreach (var scene in story.Scenes)
        {
            positions[scene.Id] = new ScenePosition(x, 0);
            x += 300;
        }

        return positions;
    }
}
